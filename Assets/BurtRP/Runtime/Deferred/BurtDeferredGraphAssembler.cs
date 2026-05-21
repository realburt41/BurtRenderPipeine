namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让 Deferred 组装器可以直接访问运行时 Pass 和资源类型。
{
    public sealed class BurtDeferredGraphAssembler : BurtRenderGraphAssembler // 定义 Deferred 渲染图组装器，当前阶段先建立可插入 GBuffer 阶段的顺序图。
    {
        private readonly BurtRenderPass allocateCameraColorPass = new BurtAllocateCameraColorPass(); // 创建 CameraColor 分配 Pass，保证 Deferred 最终仍然有中间颜色输出。
        private readonly BurtRenderPass allocateCameraDepthPass = new BurtAllocateCameraDepthPass(); // 创建 CameraDepth 分配 Pass，保证 GBuffer MRT 和后续 Forward fallback 都能共用深度。
        private readonly BurtRenderPass allocateGBuffer0Pass = new BurtAllocateGBuffer0Pass(); // 创建 GBuffer0 分配 Pass，第一版用于保存 baseColor 和 occlusion。
        private readonly BurtRenderPass allocateGBuffer1Pass = new BurtAllocateGBuffer1Pass(); // 创建 GBuffer1 分配 Pass，第一版用于保存 normal、metallic 和 smoothness。
        private readonly BurtRenderPass allocateGBuffer2Pass = new BurtAllocateGBuffer2Pass(); // 创建 GBuffer2 分配 Pass，第一版用于保存 emission 和 reflectance。
        private readonly BurtRenderPass allocateGBuffer3Pass = new BurtAllocateGBuffer3Pass();
        private readonly BurtRenderPass allocateGBuffer4Pass = new BurtAllocateGBuffer4Pass();
        private readonly BurtRenderPass allocateScreenSpaceAmbientOcclusionRawPass = new BurtAllocateScreenSpaceAmbientOcclusionRawPass();
        private readonly BurtRenderPass allocateScreenSpaceAmbientOcclusionPass = new BurtAllocateScreenSpaceAmbientOcclusionPass();
        private readonly BurtRenderPass screenSpaceAmbientOcclusionTracePass = new BurtScreenSpaceAmbientOcclusionTracePass();
        private readonly BurtRenderPass screenSpaceAmbientOcclusionBlurPass = new BurtScreenSpaceAmbientOcclusionBlurPass();
        private readonly BurtRenderPass allocateScreenSpaceGlobalIlluminationRawPass = new BurtAllocateScreenSpaceGlobalIlluminationRawPass();
        private readonly BurtRenderPass allocateScreenSpaceGlobalIlluminationPass = new BurtAllocateScreenSpaceGlobalIlluminationPass();
        private readonly BurtRenderPass allocateBurtGITemporalDiagnosticsPass = new BurtAllocateBurtGITemporalDiagnosticsPass();
        private readonly BurtRenderPass screenSpaceGlobalIlluminationTracePass = new BurtScreenSpaceGlobalIlluminationTracePass();
        private readonly BurtRenderPass screenSpaceGlobalIlluminationBlurPass = new BurtScreenSpaceGlobalIlluminationBlurPass();
        private readonly BurtRenderPass screenSpaceGlobalIlluminationCompositePass = new BurtScreenSpaceGlobalIlluminationCompositePass();
        private readonly BurtRenderPass releaseBurtGITemporalDiagnosticsPass = new BurtReleaseBurtGITemporalDiagnosticsPass();
        private readonly BurtRenderPass releaseScreenSpaceGlobalIlluminationRawPass = new BurtReleaseScreenSpaceGlobalIlluminationRawPass();
        private readonly BurtRenderPass releaseScreenSpaceGlobalIlluminationPass = new BurtReleaseScreenSpaceGlobalIlluminationPass();
        private readonly BurtRenderPass allocateHiZDepthPass = new BurtAllocateHiZDepthPass();
        private readonly BurtRenderPass allocateAdditionalLightBufferPass = new BurtAllocateRenderBufferPass(BurtRenderGraphResourceRegistry.AdditionalLightBufferName); // Allocate the graph-owned additional light buffer before lighting globals upload.
        private readonly BurtRenderPass allocateTileLightCountBufferPass = new BurtAllocateRenderBufferPass(BurtRenderGraphResourceRegistry.TileLightCountBufferName);
        private readonly BurtRenderPass allocateTileLightListBufferPass = new BurtAllocateRenderBufferPass(BurtRenderGraphResourceRegistry.TileLightListBufferName);
        private readonly BurtRenderPass allocateTileLightOffsetBufferPass = new BurtAllocateRenderBufferPass(BurtRenderGraphResourceRegistry.TileLightOffsetBufferName);
        private readonly BurtRenderPass allocateClusterLightCountBufferPass = new BurtAllocateRenderBufferPass(BurtRenderGraphResourceRegistry.ClusterLightCountBufferName);
        private readonly BurtRenderPass allocateClusterLightListBufferPass = new BurtAllocateRenderBufferPass(BurtRenderGraphResourceRegistry.ClusterLightListBufferName);
        private readonly BurtRenderPass allocateClusterLightOffsetBufferPass = new BurtAllocateRenderBufferPass(BurtRenderGraphResourceRegistry.ClusterLightOffsetBufferName);
        private readonly BurtRenderPass setupLightingPass = new BurtSetupLightingPass(); // 创建灯光全局参数上传 Pass，让 Forward 和后续 Deferred Lighting 共享同一套 LightingGlobals。
        private readonly BurtRenderPass allocateMainLightShadowMapPass = new BurtAllocateMainLightShadowMapPass(); // 创建主光阴影图分配 Pass，让 Deferred 路径继续复用现有主光阴影。
        private readonly BurtRenderPass drawMainLightShadowCasterPass = new BurtDrawMainLightShadowCasterPass(); // 创建主光 ShadowCaster 绘制 Pass，把阴影深度写入 MainLightShadowMap。
        private readonly BurtRenderPass allocateAdditionalLightShadowAtlasPass = new BurtAllocateAdditionalLightShadowAtlasPass();
        private readonly BurtRenderPass drawAdditionalLightShadowCasterPass = new BurtDrawAdditionalLightShadowCasterPass();
        private readonly BurtRenderPass seedOverlayCameraColorPass = new BurtSeedOverlayCameraColorPass(); // 创建 Overlay 颜色继承 Pass，保持非共享 Overlay 的旧兼容行为。
        private readonly BurtRenderPass setGBufferRenderTargetsPass = new BurtSetGBufferRenderTargetsPass(); // 创建 GBuffer MRT 绑定 Pass，用来验证五张 GBuffer 能被同时绑定。
        private readonly BurtRenderPass clearGBufferRenderTargetsPass = new BurtClearGBufferRenderTargetsPass(); // 创建 GBuffer 清理 Pass，用来给五张 GBuffer 写入确定的默认值。
        private readonly BurtRenderPass drawGBufferOpaquePass = new BurtDrawGBufferOpaquePass(); // 创建 GBuffer 不透明绘制 Pass，后续由 shader 侧 BurtGBuffer pass 写入材质数据。
        private readonly BurtRenderPass clearDeferredLightingTargetPass = new BurtClearDeferredLightingTargetPass(); // 创建 Deferred Lighting 黑场清理 Pass，配合 stencil 分 pass 防止跳过像素保留相机 clear color。
        private readonly BurtRenderPass deferredLitLightingPass = new BurtDeferredLitLightingPass(); // 创建 Default Lit Deferred Lighting Pass，只处理 Default Lit GBuffer 像素。
        private readonly BurtRenderPass deferredHairLightingPass = new BurtDeferredHairLightingPass(); // 创建 Hair Deferred Lighting Pass，只处理 Hair GBuffer 像素。
        private readonly BurtRenderPass deferredClearCoatLightingPass = new BurtDeferredClearCoatLightingPass();
        private readonly BurtRenderPass deferredSubsurfaceLightingPass = new BurtDeferredSubsurfaceLightingPass();
        private readonly BurtRenderPass allocateScreenSpaceSubsurfaceSourcePass = new BurtAllocateScreenSpaceSubsurfaceSourcePass();
        private readonly BurtRenderPass allocateScreenSpaceSubsurfaceBaseColorPass = new BurtAllocateScreenSpaceSubsurfaceBaseColorPass();
        private readonly BurtRenderPass allocateScreenSpaceSubsurfaceEmissionPass = new BurtAllocateScreenSpaceSubsurfaceEmissionPass();
        private readonly BurtRenderPass allocateScreenSpaceSubsurfaceSetupPass = new BurtAllocateScreenSpaceSubsurfaceSetupPass();
        private readonly BurtRenderPass allocateScreenSpaceSubsurfaceProfileIDAndTypePass = new BurtAllocateScreenSpaceSubsurfaceProfileIDAndTypePass();
        private readonly BurtRenderPass allocateScreenSpaceSubsurfaceMaskPass = new BurtAllocateScreenSpaceSubsurfaceMaskPass();
        private readonly BurtRenderPass allocateScreenSpaceSubsurfaceTempPass = new BurtAllocateScreenSpaceSubsurfaceTempPass();
        private readonly BurtRenderPass allocateScreenSpaceSubsurfaceBlurPass = new BurtAllocateScreenSpaceSubsurfaceBlurPass();
        private readonly BurtRenderPass allocateScreenSpaceSubsurfaceCombinePass = new BurtAllocateScreenSpaceSubsurfaceCombinePass();
        private readonly BurtRenderPass allocateScreenSpaceSubsurfaceHistoryPass = new BurtAllocateScreenSpaceSubsurfaceHistoryPass();
        private readonly BurtRenderPass allocateScreenSpaceSubsurfaceVelocityPass = new BurtAllocateScreenSpaceSubsurfaceVelocityPass();
        private readonly BurtRenderPass allocateScreenSpaceSubsurfaceBurleyArgsBufferPass = new BurtAllocateRenderBufferPass(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBurleyArgsBufferName);
        private readonly BurtRenderPass allocateScreenSpaceSubsurfaceBurleyGroupBufferPass = new BurtAllocateRenderBufferPass(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBurleyGroupBufferName);
        private readonly BurtRenderPass screenSpaceSubsurfaceCopySourcePass = new BurtScreenSpaceSubsurfaceCopySourcePass();
        private readonly BurtRenderPass screenSpaceSubsurfaceForwardPass = new BurtScreenSpaceSubsurfaceForwardPass();
        private readonly BurtRenderPass screenSpaceSubsurfaceBuildVelocityPass = new BurtScreenSpaceSubsurfaceBuildVelocityPass();
        private readonly BurtRenderPass screenSpaceSubsurfaceBuildMaskPass = new BurtScreenSpaceSubsurfaceBuildMaskPass();
        private readonly BurtRenderPass screenSpaceSubsurfaceInitBurleyArgsPass = new BurtScreenSpaceSubsurfaceInitBurleyArgsPass();
        private readonly BurtRenderPass screenSpaceSubsurfaceSetupPass = new BurtScreenSpaceSubsurfaceSetupPass();
        private readonly BurtRenderPass screenSpaceSubsurfaceBurleyPass = new BurtScreenSpaceSubsurfaceBurleyPass();
        private readonly BurtRenderPass screenSpaceSubsurfaceSeparableHorizontalPass = new BurtScreenSpaceSubsurfaceSeparableHorizontalPass();
        private readonly BurtRenderPass screenSpaceSubsurfaceSeparableVerticalPass = new BurtScreenSpaceSubsurfaceSeparableVerticalPass();
        private readonly BurtRenderPass screenSpaceSubsurfaceStoreHistoryPass = new BurtScreenSpaceSubsurfaceStoreHistoryPass();
        private readonly BurtRenderPass screenSpaceSubsurfaceCombinePass = new BurtScreenSpaceSubsurfaceCombinePass();
        private readonly BurtRenderPass screenSpaceSubsurfaceFinalCopyPass = new BurtScreenSpaceSubsurfaceFinalCopyPass();
        private readonly BurtRenderPass releaseScreenSpaceSubsurfaceCombinePass = new BurtReleaseScreenSpaceSubsurfaceCombinePass();
        private readonly BurtRenderPass releaseScreenSpaceSubsurfaceSourcePass = new BurtReleaseScreenSpaceSubsurfaceSourcePass();
        private readonly BurtRenderPass releaseScreenSpaceSubsurfaceBaseColorPass = new BurtReleaseScreenSpaceSubsurfaceBaseColorPass();
        private readonly BurtRenderPass releaseScreenSpaceSubsurfaceEmissionPass = new BurtReleaseScreenSpaceSubsurfaceEmissionPass();
        private readonly BurtRenderPass releaseScreenSpaceSubsurfaceSetupPass = new BurtReleaseScreenSpaceSubsurfaceSetupPass();
        private readonly BurtRenderPass releaseScreenSpaceSubsurfaceProfileIDAndTypePass = new BurtReleaseScreenSpaceSubsurfaceProfileIDAndTypePass();
        private readonly BurtRenderPass releaseScreenSpaceSubsurfaceMaskPass = new BurtReleaseScreenSpaceSubsurfaceMaskPass();
        private readonly BurtRenderPass releaseScreenSpaceSubsurfaceTempPass = new BurtReleaseScreenSpaceSubsurfaceTempPass();
        private readonly BurtRenderPass releaseScreenSpaceSubsurfaceBlurPass = new BurtReleaseScreenSpaceSubsurfaceBlurPass();
        private readonly BurtRenderPass releaseScreenSpaceSubsurfaceHistoryPass = new BurtReleaseScreenSpaceSubsurfaceHistoryPass();
        private readonly BurtRenderPass releaseScreenSpaceSubsurfaceVelocityPass = new BurtReleaseScreenSpaceSubsurfaceVelocityPass();
        private readonly BurtRenderPass releaseScreenSpaceSubsurfaceBurleyGroupBufferPass = new BurtReleaseRenderBufferPass(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBurleyGroupBufferName);
        private readonly BurtRenderPass releaseScreenSpaceSubsurfaceBurleyArgsBufferPass = new BurtReleaseRenderBufferPass(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBurleyArgsBufferName);
        private readonly BurtRenderPass setRenderTargetPass = new BurtSetRenderTargetPass(); // 创建 CameraColor/CameraDepth 绑定 Pass，GBuffer 阶段前后都会用它切回正常颜色目标。
        private readonly BurtRenderPass clearRenderTargetPass = new BurtClearRenderTargetPass(); // 创建相机清屏 Pass，保证当前 Deferred 实验模式输出仍和 Forward 一致。
        private readonly BurtRenderPass depthPrepass = new BurtDepthPrepass(); // 创建深度预写 Pass，暂时复用 Forward 的深度建立逻辑。
        private readonly BurtRenderPass drawDeferredForwardOnlyOpaquePass = new BurtDrawDeferredForwardOnlyOpaquePass(); // 创建 Deferred 后前向兜底 Pass，只绘制显式声明 BurtForwardOnly 的不透明物体。
        private readonly BurtRenderPass buildHiZDepthPass = new BurtBuildHiZDepthPass();
        private readonly BurtRenderPass buildTileLightListPass = new BurtBuildTileLightListPass();
        private readonly BurtRenderPass drawSkyboxPass = new BurtDrawSkyboxPass(); // 创建天空盒绘制 Pass，让 Deferred 实验模式仍能保留原有天空盒行为。
        private readonly BurtRenderPass drawAtmospherePass = new BurtDrawAtmospherePass();
        private readonly BurtRenderPass applyAtmosphereAerialPerspectivePass = new BurtApplyAtmosphereAerialPerspectivePass();
        private readonly BurtRenderPass applyFogPass = new BurtApplyFogPass();
        private readonly BurtRenderPass applyVolumetricFogPass = new BurtApplyVolumetricFogPass();
        private readonly BurtRenderPass allocateScreenSpaceReflectionColorPass = new BurtAllocateScreenSpaceReflectionColorPass();
        private readonly BurtRenderPass allocateScreenSpaceReflectionDenoisedColorPass = new BurtAllocateScreenSpaceReflectionDenoisedColorPass();
        private readonly BurtRenderPass allocateScreenSpaceReflectionTemporalColorPass = new BurtAllocateScreenSpaceReflectionTemporalColorPass();
        private readonly BurtRenderPass screenSpaceReflectionTracePass = new BurtScreenSpaceReflectionTracePass();
        private readonly BurtRenderPass screenSpaceReflectionDenoisePass = new BurtScreenSpaceReflectionDenoisePass();
        private readonly BurtRenderPass screenSpaceReflectionTemporalPass = new BurtScreenSpaceReflectionTemporalPass();
        private readonly BurtRenderPass screenSpaceReflectionCompositePass = new BurtScreenSpaceReflectionCompositePass();
        private readonly BurtRenderPass releaseScreenSpaceReflectionColorPass = new BurtReleaseScreenSpaceReflectionColorPass();
        private readonly BurtRenderPass releaseScreenSpaceReflectionDenoisedColorPass = new BurtReleaseScreenSpaceReflectionDenoisedColorPass();
        private readonly BurtRenderPass releaseScreenSpaceReflectionTemporalColorPass = new BurtReleaseScreenSpaceReflectionTemporalColorPass();
        private readonly BurtRenderPass drawTransparentPass = new BurtDrawTransparentPass(); // 创建透明物体绘制 Pass，未来 Deferred 第一版也会继续让透明走 Forward。
        private readonly BurtRenderPass drawUnsupportedShadersPass = new BurtDrawUnsupportedShadersPass(); // 创建不支持 Shader 调试 Pass，让非 BurtRP 材质继续显示错误材质。
        private readonly BurtRenderPass drawPreImageEffectsGizmosPass = new BurtDrawPreImageEffectsGizmosPass(); // 创建编辑器 Gizmos 绘制 Pass，恢复 SRP Scene/Game View 的 Gizmos 显示。
        private readonly BurtRenderPass drawPostImageEffectsGizmosPass = new BurtDrawPostImageEffectsGizmosPass(); // 创建后处理后的编辑器 Gizmos Pass，避免直接画到外部最终目标。
        private readonly BurtRenderPass allocatePostProcessColorPass = new BurtAllocatePostProcessColorPass(); // 创建后处理中间 RT 分配 Pass，保持后处理尾部链路不分 Forward/Deferred。
        private readonly BurtRenderPass postProcessPass = new BurtPostProcessPass(); // 创建后处理 Pass，让 Tonemapping 在 Deferred 实验模式下也能继续工作。
        private readonly BurtRenderPass releasePostProcessColorPass = new BurtReleasePostProcessColorPass(); // 创建后处理中间 RT 释放 Pass，避免后处理临时资源泄漏。
        private readonly BurtRenderPass debugCameraDepthPass = new BurtDebugCameraDepthPass(); // 创建深度调试 Pass，让 Deferred 实验模式仍能显示 CameraDepth。
        private readonly BurtRenderPass debugMainLightShadowMapPass = new BurtDebugMainLightShadowMapPass(); // 创建主光阴影图调试 Pass，让 Deferred 实验模式仍能查看 shadow map。
        private readonly BurtRenderPass debugGBufferPass = new BurtDebugGBufferPass(); // 创建 GBuffer 调试 Pass，让 Deferred 模式可以直接检查五张 GBuffer 的写入内容。
        private readonly BurtRenderPass debugHiZDepthPass = new BurtDebugHiZDepthPass();
        private readonly BurtRenderPass debugTileLightViewPass = new BurtDebugTileLightViewPass();
        private readonly BurtRenderPass debugClusterLightVolumePass = new BurtDebugClusterLightVolumePass();
        private readonly BurtRenderPass debugScreenSpaceAmbientOcclusionPass = new BurtDebugScreenSpaceAmbientOcclusionPass();
        private readonly BurtRenderPass debugScreenSpaceGlobalIlluminationPass = new BurtDebugScreenSpaceGlobalIlluminationPass();
        private readonly BurtRenderPass debugScreenSpaceSubsurfacePass = new BurtDebugScreenSpaceSubsurfacePass();
        private readonly BurtRenderPass debugScreenSpaceReflectionHiZDiagnosticsPass = new BurtScreenSpaceReflectionHiZDiagnosticsPass();
        private readonly BurtRenderPass finalBlitPass = new BurtFinalBlitPass(); // 创建最终拷贝 Pass，把 CameraColor 输出到 request 指定的最终目标。
        private readonly BurtRenderPass releaseMainLightShadowMapPass = new BurtReleaseMainLightShadowMapPass(); // 创建主光阴影图释放 Pass，结束 MainLightShadowMap 生命周期。
        private readonly BurtRenderPass releaseAdditionalLightShadowAtlasPass = new BurtReleaseAdditionalLightShadowAtlasPass();
        private readonly BurtRenderPass releaseAdditionalLightBufferPass = new BurtReleaseRenderBufferPass(BurtRenderGraphResourceRegistry.AdditionalLightBufferName); // Release the per-request additional light buffer after all lighting consumers.
        private readonly BurtRenderPass releaseTileLightCountBufferPass = new BurtReleaseRenderBufferPass(BurtRenderGraphResourceRegistry.TileLightCountBufferName);
        private readonly BurtRenderPass releaseTileLightListBufferPass = new BurtReleaseRenderBufferPass(BurtRenderGraphResourceRegistry.TileLightListBufferName);
        private readonly BurtRenderPass releaseTileLightOffsetBufferPass = new BurtReleaseRenderBufferPass(BurtRenderGraphResourceRegistry.TileLightOffsetBufferName);
        private readonly BurtRenderPass releaseClusterLightCountBufferPass = new BurtReleaseRenderBufferPass(BurtRenderGraphResourceRegistry.ClusterLightCountBufferName);
        private readonly BurtRenderPass releaseClusterLightListBufferPass = new BurtReleaseRenderBufferPass(BurtRenderGraphResourceRegistry.ClusterLightListBufferName);
        private readonly BurtRenderPass releaseClusterLightOffsetBufferPass = new BurtReleaseRenderBufferPass(BurtRenderGraphResourceRegistry.ClusterLightOffsetBufferName);
        private readonly BurtRenderPass releaseGBuffer0Pass = new BurtReleaseGBuffer0Pass(); // 创建 GBuffer0 释放 Pass，结束第一张 Deferred 缓存生命周期。
        private readonly BurtRenderPass releaseGBuffer1Pass = new BurtReleaseGBuffer1Pass(); // 创建 GBuffer1 释放 Pass，结束第二张 Deferred 缓存生命周期。
        private readonly BurtRenderPass releaseGBuffer2Pass = new BurtReleaseGBuffer2Pass(); // 创建 GBuffer2 释放 Pass，结束第三张 Deferred 缓存生命周期。
        private readonly BurtRenderPass releaseGBuffer3Pass = new BurtReleaseGBuffer3Pass();
        private readonly BurtRenderPass releaseGBuffer4Pass = new BurtReleaseGBuffer4Pass();
        private readonly BurtRenderPass releaseScreenSpaceAmbientOcclusionRawPass = new BurtReleaseScreenSpaceAmbientOcclusionRawPass();
        private readonly BurtRenderPass releaseScreenSpaceAmbientOcclusionPass = new BurtReleaseScreenSpaceAmbientOcclusionPass();
        private readonly BurtRenderPass releaseHiZDepthPass = new BurtReleaseHiZDepthPass();
        private readonly BurtRenderPass releaseCameraColorPass = new BurtReleaseCameraColorPass(); // 创建 CameraColor 释放 Pass，保持和 Forward 一致的相机颜色资源生命周期。
        private readonly BurtRenderPass releaseCameraDepthPass = new BurtReleaseCameraDepthPass(); // 创建 CameraDepth 释放 Pass，保持和 Forward 一致的相机深度资源生命周期。

        public override string Name => "Burt Deferred Graph Assembler"; // 返回当前组装器名称，方便日志和调试工具区分 Forward/Deferred。

        public override void Assemble( // 实现旧组装入口，兼容没有传入相机栈执行选项的调用方。
            BurtRenderGraph graph, // 接收要写入 Pass 的 RenderGraph。
            BurtRenderRequest request, // 接收当前正在组装的渲染请求。
            BurtRenderPipelineAsset asset) // 接收管线资产配置，用来读取 Renderer Mode、阴影和后处理开关。
        {
            Assemble(graph, request, asset, BurtRequestRenderOptions.CreateSingleRequest()); // 把旧入口转发到新入口，并使用单 request 生命周期作为默认行为。
        }

        public override void Assemble( // 实现带相机栈执行选项的组装入口。
            BurtRenderGraph graph, // 接收要写入 Pass 的 RenderGraph。
            BurtRenderRequest request, // 接收当前正在组装的渲染请求。
            BurtRenderPipelineAsset asset, // 接收管线资产配置，用来读取 Depth Prepass、Debug 和后处理开关。
            BurtRequestRenderOptions renderOptions) // 接收当前 request 的相机栈 RT 生命周期选项。
        {
            if (!CanAssemble(graph, request)) // 先检查 graph、request 和 camera 是否满足组装条件。
            {
                return; // 输入无效时直接结束，避免后续 Pass 访问空对象。
            }

            var safeRenderOptions = renderOptions ?? BurtRequestRenderOptions.CreateSingleRequest(); // renderOptions 为空时回退到单 request 行为，避免所有生命周期标记都缺失。
            var useLocalGBufferTargets = ShouldUseLocalGBufferTargets(safeRenderOptions); // 当前阶段让 GBuffer 生命周期跟随本 request 的 CameraColor/CameraDepth 申请。
            var useHiZDepth = ShouldUseHiZDepth(request, asset, useLocalGBufferTargets); // Only allocate and build HiZ when SSR or HiZ debug needs it.
            var useMainLightShadow = BurtShadowUtility.ShouldUseMainLightShadow(request, asset); // 复用阴影工具判断当前 request 是否需要主光阴影图。
            var useAdditionalLightShadow = BurtAdditionalLightShadowUtility.ShouldUseAdditionalLightShadows(request);
            AddCameraAllocationPasses(graph, safeRenderOptions); // 先申请 CameraColor 和 CameraDepth，确保 GBuffer MRT 可以使用独立深度。
            AddGBufferAllocationPasses(graph, useLocalGBufferTargets); // 再申请五张 GBuffer，给后面的 MRT 绑定和清理阶段准备真实 RT。
            AddHiZAllocationPass(graph, useHiZDepth);

            graph.AddPass(allocateAdditionalLightBufferPass); // Allocate the additional light GPU buffer before Setup Lighting uploads packed rows.
            AddTileLightBufferAllocationPasses(graph, request, asset, useLocalGBufferTargets);
            AddClusterLightBufferAllocationPasses(graph, request, asset, useLocalGBufferTargets);
            graph.AddPass(setupLightingPass); // 上传灯光和默认阴影全局参数，后续 Forward fallback 与 Deferred Lighting 都会依赖它。
            AddTileLightBuildPass(graph, request, asset, useLocalGBufferTargets);
            AddShadowPasses(graph, useMainLightShadow, useAdditionalLightShadow); // 如果需要阴影，就绘制 shadow map。
            var mainLightShadowMapIsValid = graph.Resources.GetMainLightShadowMap().IsValid; // 在阴影 Pass 注册之后读取资源句柄状态，诊断才代表最终图结构。
            BurtShadowUtility.LogMainLightShadowDiagnostics(request, asset, useMainLightShadow, mainLightShadowMapIsValid); // 输出主光阴影诊断，保持 Forward 和 Deferred 的排查方式一致。

            if (ShouldSeedOverlayCameraColor(request, safeRenderOptions)) // 非共享 Overlay 且不清颜色时，需要继承最终目标内容。
            {
                graph.AddPass(seedOverlayCameraColorPass); // 把已有最终目标复制到 CameraColor，保持 Overlay 旧兼容行为。
            }

            graph.AddPass(setRenderTargetPass); // 先绑定 CameraColor/CameraDepth，保证清屏和 Depth Prepass 写入相机自己的中间目标。
            graph.AddPass(clearRenderTargetPass); // 按当前相机清屏配置清理 CameraColor/CameraDepth，保持实验模式画面和 Forward 一致。
            AddDepthPrepass(graph, asset); // 根据资产开关决定是否加入深度预写。
            AddGBufferBootstrapPasses(graph, useLocalGBufferTargets); // 在相机清屏和深度预写后插入 GBuffer MRT 绑定和清理阶段。
            AddDrawGBufferOpaquePass(graph, useLocalGBufferTargets); // 绘制支持 BurtGBuffer pass 的不透明物体，当前 shader 侧没有时会自然为空。
            AddReturnToCameraColorPass(graph, useLocalGBufferTargets); // GBuffer 阶段完成后重新绑定 CameraColor，避免 Forward fallback 继续画进 GBuffer。

            AddScreenSpaceAmbientOcclusionPasses(graph, request, asset, useLocalGBufferTargets);
            AddDeferredLightingPass(graph, request, asset, useLocalGBufferTargets); // 使用 GBuffer 合成不透明物体光照，CameraColor 从这里开始进入真正 Deferred 不透明结果。
            AddScreenSpaceGlobalIlluminationPasses(graph, request, asset, useLocalGBufferTargets);
            AddDeferredForwardOnlyOpaqueFallback(graph, asset); // 根据资产开关决定是否绘制不能写入 GBuffer 的前向专用不透明物体。
            AddHiZBuildPass(graph, useHiZDepth);
            AddScreenSpaceSubsurfacePasses(graph, request, asset, useLocalGBufferTargets);
            if (BurtAtmosphereUtility.ShouldApplyAerialPerspectiveAfterOpaqueBeforeSky(request))
            {
                graph.AddPass(applyAtmosphereAerialPerspectivePass);
            }

            if (BurtFogUtility.ShouldUseFog(request))
            {
                graph.AddPass(applyFogPass);
            }

            graph.AddPass(drawSkyboxPass); // 在不透明之后绘制天空盒，保持 Forward 现有顺序。
            if (BurtAtmosphereUtility.ShouldUseAtmosphere(request))
            {
                graph.AddPass(drawAtmospherePass);
            }
            if (BurtAtmosphereUtility.ShouldApplyAerialPerspectiveAfterSkyBeforeSSR(request))
            {
                graph.AddPass(applyAtmosphereAerialPerspectivePass);
            }
            AddScreenSpaceReflectionPasses(graph, request, asset, useLocalGBufferTargets);
            if (BurtAtmosphereUtility.ShouldApplyAerialPerspectiveBeforeTransparent(request))
            {
                graph.AddPass(applyAtmosphereAerialPerspectivePass);
            }
            if (BurtVolumetricFogUtility.ShouldUseVolumetricFog(request))
            {
                graph.AddPass(applyVolumetricFogPass);
            }

            graph.AddPass(drawTransparentPass); // 透明物体继续走 Forward，未来 Deferred 第一版也会保持这个策略。
            AddUnsupportedShaderDebug(graph, asset); // 根据资产开关决定是否绘制不支持 Shader 的错误材质。
            AddPreImageEffectsGizmosPass(graph, request); // 编辑器里在后处理前恢复 PreImageEffects Gizmos。
            AddPostProcessPasses(graph, request, asset, safeRenderOptions); // 根据后处理和 FinalBlit 条件决定是否插入 Tonemapping 链路。
            AddDebugViewPasses(graph, request, asset, useMainLightShadow, useLocalGBufferTargets); // 根据 Debug 开关决定是否覆盖显示深度、主光阴影或 GBuffer。
            AddPostImageEffectsGizmosPass(graph, request); // 后处理和 Debug 覆盖之后，把 PostImageEffects Gizmos 画回 CameraColor。

            if (safeRenderOptions.ShouldFinalBlit) // 只有当前 request 是最终输出点时才执行 FinalBlit。
            {
                graph.AddPass(finalBlitPass); // 把 CameraColor 拷贝到 request.TargetIdentifier。
            }

            AddTileLightBufferReleasePasses(graph, request, asset, useLocalGBufferTargets);
            AddClusterLightBufferReleasePasses(graph, request, asset, useLocalGBufferTargets);
            AddAdditionalLightBufferReleasePass(graph); // End the packed additional-light buffer lifetime after all shading consumers.
            AddShadowReleasePasses(graph, useMainLightShadow, useAdditionalLightShadow); // 释放阴影图，结束阴影资源生命周期。
            AddHiZReleasePass(graph, useHiZDepth);
            AddScreenSpaceAmbientOcclusionReleasePasses(graph, request, asset, useLocalGBufferTargets);
            AddGBufferReleasePasses(graph, useLocalGBufferTargets); // 释放本 request 内申请的 GBuffer，当前阶段不跨 request 保留它们。
            AddCameraReleasePasses(graph, safeRenderOptions); // 最后按相机栈策略释放 CameraColor 和 CameraDepth。
        }

        private static bool CanAssemble( // 判断当前输入是否允许组装 Deferred RenderGraph。
            BurtRenderGraph graph, // 接收待检查的 RenderGraph。
            BurtRenderRequest request) // 接收待检查的渲染请求。
        {
            if (graph == null) // graph 为空说明调用方没有提供可写入的图。
            {
                return false; // 返回 false，阻止后续组装。
            }

            if (request == null) // request 为空说明没有有效渲染任务。
            {
                return false; // 返回 false，阻止后续组装。
            }

            if (!request.IsValid) // request 标记为无效时不应该被渲染。
            {
                return false; // 返回 false，避免给无效 request 添加 Pass。
            }

            return request.Camera != null; // 只有 request 拥有真实 Camera 时才允许组装后续 Pass。
        }

        private void AddCameraAllocationPasses( // 按相机栈生命周期添加 CameraColor 和 CameraDepth 分配 Pass。
            BurtRenderGraph graph, // 接收要写入 Pass 的 RenderGraph。
            BurtRequestRenderOptions renderOptions) // 接收当前 request 的 RT 生命周期选项。
        {
            if (renderOptions.ShouldAllocateCameraColor) // 当前 request 需要负责申请 CameraColor 时才添加分配 Pass。
            {
                graph.AddPass(allocateCameraColorPass); // 添加 CameraColor 分配 Pass。
            }

            if (renderOptions.ShouldAllocateCameraDepth) // 当前 request 需要负责申请 CameraDepth 时才添加分配 Pass。
            {
                graph.AddPass(allocateCameraDepthPass); // 添加 CameraDepth 分配 Pass。
            }
        }

        private void AddGBufferAllocationPasses( // 添加五张 GBuffer 的分配 Pass。
            BurtRenderGraph graph, // 接收要写入 Pass 的 RenderGraph。
            bool useLocalGBufferTargets) // 接收当前 request 是否需要本地图内 GBuffer 生命周期。
        {
            if (!useLocalGBufferTargets) // 当前 request 不负责申请 CameraColor/CameraDepth 时，也暂时不申请 GBuffer。
            {
                return; // 直接返回，避免在共享栈的 Overlay request 上申请无用 GBuffer。
            }

            graph.AddPass(allocateGBuffer0Pass); // 添加 GBuffer0 分配 Pass。
            graph.AddPass(allocateGBuffer1Pass); // 添加 GBuffer1 分配 Pass。
            graph.AddPass(allocateGBuffer2Pass); // 添加 GBuffer2 分配 Pass。
            graph.AddPass(allocateGBuffer3Pass);
            graph.AddPass(allocateGBuffer4Pass);
        }

        private void AddHiZAllocationPass(
            BurtRenderGraph graph,
            bool useHiZDepth)
        {
            if (!useHiZDepth)
            {
                return;
            }

            graph.AddPass(allocateHiZDepthPass);
        }

        private void AddTileLightBufferAllocationPasses(
            BurtRenderGraph graph,
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset,
            bool useLocalGBufferTargets)
        {
            if (!BurtTiledLightData.ShouldUseTiledLightResources(request, asset, useLocalGBufferTargets))
            {
                return;
            }

            graph.AddPass(allocateTileLightCountBufferPass);
            if (BurtTiledLightData.ShouldUseTileLightListResources(request, asset, useLocalGBufferTargets))
            {
                graph.AddPass(allocateTileLightListBufferPass);
                graph.AddPass(allocateTileLightOffsetBufferPass);
            }
        }

        private void AddTileLightBuildPass(
            BurtRenderGraph graph,
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset,
            bool useLocalGBufferTargets)
        {
            if (!BurtTiledLightData.ShouldUseTiledLightResources(request, asset, useLocalGBufferTargets) &&
                !BurtTiledLightData.ShouldUseClusterLightResources(request, asset, useLocalGBufferTargets))
            {
                return;
            }

            graph.AddPass(buildTileLightListPass);
        }

        private void AddClusterLightBufferAllocationPasses(
            BurtRenderGraph graph,
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset,
            bool useLocalGBufferTargets)
        {
            if (!BurtTiledLightData.ShouldUseClusterLightResources(request, asset, useLocalGBufferTargets))
            {
                return;
            }

            graph.AddPass(allocateClusterLightCountBufferPass);
            graph.AddPass(allocateClusterLightListBufferPass);
            graph.AddPass(allocateClusterLightOffsetBufferPass);
        }

        private void AddShadowPasses( // 添加主光阴影生成 Pass。
            BurtRenderGraph graph, // 接收要写入 Pass 的 RenderGraph。
            bool useMainLightShadow, // 接收当前 request 是否启用主光阴影。
            bool useAdditionalLightShadow)
        {
            if (!useMainLightShadow && !useAdditionalLightShadow) // 没有阴影时不需要申请或绘制 shadow map。
            {
                return; // 直接返回，减少无意义 Pass。
            }

            if (useMainLightShadow)
            {
                graph.AddPass(allocateMainLightShadowMapPass); // 添加主光阴影图分配 Pass。
                graph.AddPass(drawMainLightShadowCasterPass); // 添加主光 ShadowCaster 绘制 Pass。
            }

            if (useAdditionalLightShadow)
            {
                graph.AddPass(allocateAdditionalLightShadowAtlasPass);
                graph.AddPass(drawAdditionalLightShadowCasterPass);
            }
        }

        private void AddGBufferBootstrapPasses( // 添加 GBuffer MRT 结构验证 Pass。
            BurtRenderGraph graph, // 接收要写入 Pass 的 RenderGraph。
            bool useLocalGBufferTargets) // 接收当前 request 是否拥有本地图内 GBuffer。
        {
            if (!useLocalGBufferTargets) // 没有本地 GBuffer 生命周期时不能安全绑定 MRT。
            {
                return; // 直接返回，避免在没有申请 GBuffer 的 request 上绑定无效 RT。
            }

            graph.AddPass(setGBufferRenderTargetsPass); // 添加 MRT 绑定 Pass，验证五张 GBuffer 可以同时作为颜色目标。
            graph.AddPass(clearGBufferRenderTargetsPass); // 添加 GBuffer 清理 Pass，给 GBuffer 写入稳定默认值。
        }

        private void AddDrawGBufferOpaquePass( // 添加不透明 GBuffer 绘制 Pass。
            BurtRenderGraph graph, // 接收要写入 Pass 的 RenderGraph。
            bool useLocalGBufferTargets) // 接收当前 request 是否拥有本地图内 GBuffer。
        {
            if (!useLocalGBufferTargets) // 没有本地 GBuffer 生命周期时不能安全绘制 GBuffer。
            {
                return; // 直接返回，避免 DrawRenderers 绑定无效 MRT。
            }

            graph.AddPass(drawGBufferOpaquePass); // 添加 GBuffer 不透明绘制 Pass。
        }

        private void AddReturnToCameraColorPass( // 在 GBuffer 阶段后重新绑定相机颜色目标。
            BurtRenderGraph graph, // 接收要写入 Pass 的 RenderGraph。
            bool useLocalGBufferTargets) // 接收当前 request 是否刚刚执行过 GBuffer 阶段。
        {
            if (!useLocalGBufferTargets) // 如果没有切换到 GBuffer MRT，就不需要额外切回 CameraColor。
            {
                return; // 直接返回，保持当前 CameraColor 绑定状态不变。
            }

            graph.AddPass(setRenderTargetPass); // 添加一次 CameraColor/CameraDepth 绑定 Pass，确保后续 Forward fallback 输出到正常相机颜色。
        }

        private void AddDeferredForwardOnlyOpaqueFallback( // 添加 Deferred 后的 ForwardOnly 不透明兜底绘制。
            BurtRenderGraph graph, // 接收要写入 Pass 的 RenderGraph。
            BurtRenderPipelineAsset asset) // 接收管线资产配置，用来读取是否启用 ForwardOnly fallback。
        {
            if (!ShouldUseDeferredForwardOnlyOpaqueFallback(asset)) // 如果资产关闭了兜底绘制，就让画面完全依赖 Deferred Lighting 和后续透明 Forward。
            {
                return; // 直接返回，避免额外绘制 ForwardOnly 不透明物体。
            }

            graph.AddPass(drawDeferredForwardOnlyOpaquePass); // 添加 Deferred 后前向兜底 Pass，只会绘制 LightMode=BurtForwardOnly 的物体。
        }

        private void AddHiZBuildPass(
            BurtRenderGraph graph,
            bool useHiZDepth)
        {
            if (!useHiZDepth)
            {
                return;
            }

            graph.AddPass(buildHiZDepthPass);
        }

        private void AddScreenSpaceAmbientOcclusionPasses(
            BurtRenderGraph graph,
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset,
            bool useLocalGBufferTargets)
        {
            if (!useLocalGBufferTargets || !BurtScreenSpaceAmbientOcclusionPassUtility.ShouldUseScreenSpaceAmbientOcclusion(request, asset))
            {
                return;
            }

            graph.AddPass(allocateScreenSpaceAmbientOcclusionRawPass);
            graph.AddPass(allocateScreenSpaceAmbientOcclusionPass);
            graph.AddPass(screenSpaceAmbientOcclusionTracePass);
            graph.AddPass(screenSpaceAmbientOcclusionBlurPass);
        }

        private void AddScreenSpaceReflectionPasses(
            BurtRenderGraph graph,
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset,
            bool useLocalGBufferTargets)
        {
            if (!useLocalGBufferTargets || !BurtScreenSpaceReflectionPassUtility.ShouldUseScreenSpaceReflections(request, asset))
            {
                return;
            }

            graph.AddPass(allocateScreenSpaceReflectionColorPass);
            graph.AddPass(allocateScreenSpaceReflectionDenoisedColorPass);
            graph.AddPass(allocateScreenSpaceReflectionTemporalColorPass);
            graph.AddPass(screenSpaceReflectionTracePass);
            graph.AddPass(screenSpaceReflectionDenoisePass);
            graph.AddPass(screenSpaceReflectionTemporalPass);
            graph.AddPass(screenSpaceReflectionCompositePass);
            graph.AddPass(releaseScreenSpaceReflectionTemporalColorPass);
            graph.AddPass(releaseScreenSpaceReflectionDenoisedColorPass);
            graph.AddPass(releaseScreenSpaceReflectionColorPass);
        }

        private void AddScreenSpaceGlobalIlluminationPasses(
            BurtRenderGraph graph,
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset,
            bool useLocalGBufferTargets)
        {
            if (!useLocalGBufferTargets || !BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIllumination(request, asset))
            {
                return;
            }

            var useBurtGITemporalDiagnostics = BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIlluminationTemporalDiagnostics(request, asset);
            var useBurtGIDebugView = BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIlluminationDebugView(request, asset);

            graph.AddPass(allocateScreenSpaceGlobalIlluminationRawPass);
            graph.AddPass(allocateScreenSpaceGlobalIlluminationPass);
            if (useBurtGITemporalDiagnostics)
            {
                graph.AddPass(allocateBurtGITemporalDiagnosticsPass);
            }

            graph.AddPass(screenSpaceGlobalIlluminationTracePass);
            graph.AddPass(screenSpaceGlobalIlluminationBlurPass);
            graph.AddPass(screenSpaceGlobalIlluminationCompositePass);
            if (!useBurtGIDebugView)
            {
                if (useBurtGITemporalDiagnostics)
                {
                    graph.AddPass(releaseBurtGITemporalDiagnosticsPass);
                }

                graph.AddPass(releaseScreenSpaceGlobalIlluminationPass);
                graph.AddPass(releaseScreenSpaceGlobalIlluminationRawPass);
            }
        }

        private void AddScreenSpaceSubsurfacePasses(
            BurtRenderGraph graph,
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset,
            bool useLocalGBufferTargets)
        {
            if (!useLocalGBufferTargets || !BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(request, asset))
            {
                return;
            }

            var useScreenSpaceSubsurfaceMaskTexture = BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceMaskTexture(request, asset);
            var useScreenSpaceSubsurfaceBurley = BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceBurley(request, asset);
            var useScreenSpaceSubsurfaceSeparable = BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceSeparable(request, asset);

            graph.AddPass(allocateScreenSpaceSubsurfaceSourcePass);
            graph.AddPass(allocateScreenSpaceSubsurfaceBaseColorPass);
            graph.AddPass(allocateScreenSpaceSubsurfaceEmissionPass);
            graph.AddPass(allocateScreenSpaceSubsurfaceSetupPass);
            graph.AddPass(allocateScreenSpaceSubsurfaceProfileIDAndTypePass);
            if (useScreenSpaceSubsurfaceMaskTexture)
            {
                graph.AddPass(allocateScreenSpaceSubsurfaceMaskPass);
            }

            graph.AddPass(allocateScreenSpaceSubsurfaceTempPass);
            graph.AddPass(allocateScreenSpaceSubsurfaceBlurPass);
            graph.AddPass(allocateScreenSpaceSubsurfaceCombinePass);
            if (useScreenSpaceSubsurfaceBurley)
            {
                graph.AddPass(allocateScreenSpaceSubsurfaceBurleyArgsBufferPass);
                graph.AddPass(allocateScreenSpaceSubsurfaceBurleyGroupBufferPass);
                graph.AddPass(allocateScreenSpaceSubsurfaceHistoryPass);
                graph.AddPass(allocateScreenSpaceSubsurfaceVelocityPass);
            }

            graph.AddPass(screenSpaceSubsurfaceCopySourcePass);
            graph.AddPass(screenSpaceSubsurfaceForwardPass);
            if (useScreenSpaceSubsurfaceBurley)
            {
                graph.AddPass(screenSpaceSubsurfaceBuildVelocityPass);
            }

            if (useScreenSpaceSubsurfaceMaskTexture)
            {
                graph.AddPass(screenSpaceSubsurfaceBuildMaskPass);
            }

            if (useScreenSpaceSubsurfaceBurley)
            {
                graph.AddPass(screenSpaceSubsurfaceInitBurleyArgsPass);
            }

            graph.AddPass(screenSpaceSubsurfaceSetupPass);
            if (useScreenSpaceSubsurfaceBurley)
            {
                graph.AddPass(screenSpaceSubsurfaceBurleyPass);
            }

            if (useScreenSpaceSubsurfaceSeparable)
            {
                graph.AddPass(screenSpaceSubsurfaceSeparableHorizontalPass);
                graph.AddPass(screenSpaceSubsurfaceSeparableVerticalPass);
            }

            if (useScreenSpaceSubsurfaceBurley)
            {
                graph.AddPass(screenSpaceSubsurfaceStoreHistoryPass);
            }

            graph.AddPass(screenSpaceSubsurfaceCombinePass);
            graph.AddPass(screenSpaceSubsurfaceFinalCopyPass);
            graph.AddPass(releaseScreenSpaceSubsurfaceCombinePass);
            if (useScreenSpaceSubsurfaceBurley)
            {
                graph.AddPass(releaseScreenSpaceSubsurfaceHistoryPass);
            }

            graph.AddPass(releaseScreenSpaceSubsurfaceBlurPass);
            graph.AddPass(releaseScreenSpaceSubsurfaceTempPass);
            if (useScreenSpaceSubsurfaceMaskTexture)
            {
                graph.AddPass(releaseScreenSpaceSubsurfaceMaskPass);
            }

            graph.AddPass(releaseScreenSpaceSubsurfaceProfileIDAndTypePass);
            graph.AddPass(releaseScreenSpaceSubsurfaceSetupPass);
            if (useScreenSpaceSubsurfaceBurley)
            {
                graph.AddPass(releaseScreenSpaceSubsurfaceVelocityPass);
            }

            graph.AddPass(releaseScreenSpaceSubsurfaceEmissionPass);
            graph.AddPass(releaseScreenSpaceSubsurfaceBaseColorPass);
            graph.AddPass(releaseScreenSpaceSubsurfaceSourcePass);
            if (useScreenSpaceSubsurfaceBurley)
            {
                graph.AddPass(releaseScreenSpaceSubsurfaceBurleyGroupBufferPass);
                graph.AddPass(releaseScreenSpaceSubsurfaceBurleyArgsBufferPass);
            }
        }

        private void AddDeferredLightingPass( // 添加 Deferred Lighting 全屏合成 Pass。
            BurtRenderGraph graph, // 接收要写入 Pass 的 RenderGraph。
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset,
            bool useLocalGBufferTargets) // 接收当前 request 是否拥有本地图内 GBuffer。
        {
            if (!useLocalGBufferTargets) // 没有本地 GBuffer 生命周期时不能执行 Deferred Lighting。
            {
                return; // 直接返回，避免 Deferred Lighting 读取无效 GBuffer。
            }

            graph.AddPass(clearDeferredLightingTargetPass); // 先把 lighting target 清黑；后续 stencil pass 跳过的像素不会继承相机 clear color。
            graph.AddPass(deferredLitLightingPass); // 先写入 Default Lit 像素；后续 shading model pass 以 additive 方式补齐专用模型。
            graph.AddPass(deferredHairLightingPass); // 叠加 Hair 像素；shader pass 和 GBuffer model id 都使用 1。
            if (ShouldUseDeferredClearCoatLighting(request, asset, useLocalGBufferTargets))
            {
                graph.AddPass(deferredClearCoatLightingPass);
            }
            graph.AddPass(deferredSubsurfaceLightingPass);
        }

        private void AddDepthPrepass( // 根据资产开关添加深度预写 Pass。
            BurtRenderGraph graph, // 接收要写入 Pass 的 RenderGraph。
            BurtRenderPipelineAsset asset) // 接收管线资产配置。
        {
            if (!ShouldUseDepthPrepass(asset)) // 如果资产关闭 Depth Prepass，就不添加这个 Pass。
            {
                return; // 直接返回，保持 Inspector 开关生效。
            }

            graph.AddPass(depthPrepass); // 添加深度预写 Pass。
        }

        private void AddUnsupportedShaderDebug( // 根据资产开关添加不支持 Shader 调试 Pass。
            BurtRenderGraph graph, // 接收要写入 Pass 的 RenderGraph。
            BurtRenderPipelineAsset asset) // 接收管线资产配置。
        {
            if (!ShouldUseUnsupportedShaderDebug(asset)) // 如果资产关闭错误材质调试，就不添加这个 Pass。
            {
                return; // 直接返回，避免额外绘制。
            }

            graph.AddPass(drawUnsupportedShadersPass); // 添加不支持 Shader 调试 Pass。
        }

        private void AddPreImageEffectsGizmosPass( // 根据编辑器 Gizmos 开关添加 PreImageEffects Gizmos Pass。
            BurtRenderGraph graph, // 接收要写入 Pass 的 RenderGraph。
            BurtRenderRequest request) // 接收当前 request，用来过滤 Preview/Reflection 和 Gizmos 开关。
        {
            if (!BurtEditorGizmoUtility.ShouldRenderGizmos(request)) // 非编辑器、Preview/Reflection 或关闭 Gizmos 时不添加这个 Pass。
            {
                return; // 直接返回，避免运行时和无 Gizmos 场景出现空 Pass。
            }

            graph.AddPass(drawPreImageEffectsGizmosPass); // 添加 PreImageEffects Gizmos Pass。
        }

        private void AddPostImageEffectsGizmosPass( // 根据编辑器 Gizmos 开关添加 PostImageEffects Gizmos Pass。
            BurtRenderGraph graph, // 接收要写入 Pass 的 RenderGraph。
            BurtRenderRequest request) // 接收当前 request，用来过滤 Preview/Reflection 和 Gizmos 开关。
        {
            if (!BurtEditorGizmoUtility.ShouldRenderGizmos(request)) // 非编辑器、Preview/Reflection 或关闭 Gizmos 时不添加这个 Pass。
            {
                return; // 直接返回，避免运行时和无 Gizmos 场景出现空 Pass。
            }

            graph.AddPass(drawPostImageEffectsGizmosPass); // 添加 PostImageEffects Gizmos Pass。
        }

        private void AddPostProcessPasses( // 根据后处理条件添加后处理链路。
            BurtRenderGraph graph, // 接收要写入 Pass 的 RenderGraph。
            BurtRenderRequest request, // 接收当前 request。
            BurtRenderPipelineAsset asset, // 接收管线资产配置。
            BurtRequestRenderOptions renderOptions) // 接收当前 request 的 RT 生命周期选项。
        {
            if (!ShouldUsePostProcessFramework(request, asset, renderOptions)) // 如果后处理不启用或当前 request 不是最终输出点，就不插入后处理。
            {
                return; // 直接返回，避免无意义的 PostProcessColor 分配。
            }

            graph.AddPass(allocatePostProcessColorPass); // 添加后处理中间颜色 RT 分配 Pass。
            graph.AddPass(postProcessPass); // 添加后处理 Pass，执行 No-op Copy 或 Tonemapping。
            graph.AddPass(releasePostProcessColorPass); // 添加后处理中间颜色 RT 释放 Pass。
        }

        private void AddDebugViewPasses( // 根据资产开关添加调试视图 Pass。
            BurtRenderGraph graph, // 接收要写入 Pass 的 RenderGraph。
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset, // 接收管线资产配置。
            bool useMainLightShadow, // 接收当前 request 是否真的生成了主光阴影。
            bool useLocalGBufferTargets) // 接收当前 request 是否拥有可读取的本地 GBuffer。
        {
            if (ShouldUseDepthDebugView(asset)) // 如果开启 CameraDepth Debug View，就在 FinalBlit 前覆盖 CameraColor。
            {
                graph.AddPass(debugCameraDepthPass); // 添加 CameraDepth 调试 Pass。
            }

            if (ShouldUseMainLightShadowDebugView(asset, useMainLightShadow)) // 如果开启主光阴影图 Debug View 且当前 request 有 shadow map，就覆盖 CameraColor。
            {
                graph.AddPass(debugMainLightShadowMapPass); // 添加主光 shadow map 调试 Pass。
            }

            if (ShouldUseGBufferDebugView(asset, useLocalGBufferTargets)) // 如果 Deferred 模式开启了 GBuffer Debug 且当前 request 有 GBuffer，就覆盖 CameraColor。
            {
                graph.AddPass(debugGBufferPass); // 添加 GBuffer 调试 Pass，显示原始或解码后的 Deferred 缓存内容。
            }

            if (ShouldUseHiZDebugView(asset, useLocalGBufferTargets))
            {
                graph.AddPass(debugHiZDepthPass);
            }

            if (asset != null && asset.RendererMode == BurtRendererMode.Deferred && BurtTileLightDebugViewUtility.ShouldUseTileLightDebugView(useLocalGBufferTargets))
            {
                graph.AddPass(debugTileLightViewPass);
            }

            if (asset != null && asset.RendererMode == BurtRendererMode.Deferred && BurtTileLightDebugViewUtility.ShouldUseClusterLightDebugView(useLocalGBufferTargets))
            {
                graph.AddPass(debugClusterLightVolumePass);
            }

            if (ShouldUseScreenSpaceAmbientOcclusionDebugView(request, asset, useLocalGBufferTargets))
            {
                graph.AddPass(debugScreenSpaceAmbientOcclusionPass);
            }

            if (ShouldUseScreenSpaceGlobalIlluminationDebugView(request, asset, useLocalGBufferTargets))
            {
                graph.AddPass(debugScreenSpaceGlobalIlluminationPass);
                if (BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIlluminationTemporalDiagnostics(request, asset))
                {
                    graph.AddPass(releaseBurtGITemporalDiagnosticsPass);
                }

                graph.AddPass(releaseScreenSpaceGlobalIlluminationPass);
                graph.AddPass(releaseScreenSpaceGlobalIlluminationRawPass);
            }

            if (ShouldUseScreenSpaceSubsurfaceDebugView(request, asset, useLocalGBufferTargets))
            {
                graph.AddPass(debugScreenSpaceSubsurfacePass);
            }

            if (ShouldUseScreenSpaceReflectionHiZDiagnosticView(request, asset, useLocalGBufferTargets))
            {
                graph.AddPass(debugScreenSpaceReflectionHiZDiagnosticsPass);
            }
        }

        private void AddAdditionalLightBufferReleasePass(BurtRenderGraph graph)
        {
            graph.AddPass(releaseAdditionalLightBufferPass);
        }

        private void AddTileLightBufferReleasePasses(
            BurtRenderGraph graph,
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset,
            bool useLocalGBufferTargets)
        {
            if (!BurtTiledLightData.ShouldUseTiledLightResources(request, asset, useLocalGBufferTargets))
            {
                return;
            }

            if (BurtTiledLightData.ShouldUseTileLightListResources(request, asset, useLocalGBufferTargets))
            {
                graph.AddPass(releaseTileLightOffsetBufferPass);
                graph.AddPass(releaseTileLightListBufferPass);
            }

            graph.AddPass(releaseTileLightCountBufferPass);
        }

        private void AddClusterLightBufferReleasePasses(
            BurtRenderGraph graph,
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset,
            bool useLocalGBufferTargets)
        {
            if (!BurtTiledLightData.ShouldUseClusterLightResources(request, asset, useLocalGBufferTargets))
            {
                return;
            }

            graph.AddPass(releaseClusterLightOffsetBufferPass);
            graph.AddPass(releaseClusterLightListBufferPass);
            graph.AddPass(releaseClusterLightCountBufferPass);
        }

        private void AddShadowReleasePasses( // 添加主光阴影释放 Pass。
            BurtRenderGraph graph, // 接收要写入 Pass 的 RenderGraph。
            bool useMainLightShadow, // 接收当前 request 是否启用主光阴影。
            bool useAdditionalLightShadow)
        {
            if (!useMainLightShadow && !useAdditionalLightShadow) // 没有申请阴影图时不需要释放。
            {
                return; // 直接返回，避免释放不存在的临时 RT。
            }

            if (useMainLightShadow)
            {
                graph.AddPass(releaseMainLightShadowMapPass); // 添加主光阴影图释放 Pass。
            }

            if (useAdditionalLightShadow)
            {
                graph.AddPass(releaseAdditionalLightShadowAtlasPass);
            }
        }

        private void AddHiZReleasePass(
            BurtRenderGraph graph,
            bool useHiZDepth)
        {
            if (!useHiZDepth)
            {
                return;
            }

            graph.AddPass(releaseHiZDepthPass);
        }

        private void AddScreenSpaceAmbientOcclusionReleasePasses(
            BurtRenderGraph graph,
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset,
            bool useLocalGBufferTargets)
        {
            if (!useLocalGBufferTargets || !BurtScreenSpaceAmbientOcclusionPassUtility.ShouldUseScreenSpaceAmbientOcclusion(request, asset))
            {
                return;
            }

            graph.AddPass(releaseScreenSpaceAmbientOcclusionPass);
            graph.AddPass(releaseScreenSpaceAmbientOcclusionRawPass);
        }

        private void AddGBufferReleasePasses( // 添加五张 GBuffer 的释放 Pass。
            BurtRenderGraph graph, // 接收要写入 Pass 的 RenderGraph。
            bool useLocalGBufferTargets) // 接收当前 request 是否申请过本地 GBuffer。
        {
            if (!useLocalGBufferTargets) // 当前 request 没有申请 GBuffer 时不需要释放。
            {
                return; // 直接返回，避免释放不存在的临时 RT。
            }

            graph.AddPass(releaseGBuffer4Pass);
            graph.AddPass(releaseGBuffer3Pass);
            graph.AddPass(releaseGBuffer2Pass); // 先释放 GBuffer2，和申请顺序相反，方便观察生命周期。
            graph.AddPass(releaseGBuffer1Pass); // 再释放 GBuffer1。
            graph.AddPass(releaseGBuffer0Pass); // 最后释放 GBuffer0。
        }

        private void AddCameraReleasePasses( // 按相机栈生命周期添加 CameraColor 和 CameraDepth 释放 Pass。
            BurtRenderGraph graph, // 接收要写入 Pass 的 RenderGraph。
            BurtRequestRenderOptions renderOptions) // 接收当前 request 的 RT 生命周期选项。
        {
            if (renderOptions.ShouldReleaseCameraColor) // 当前 request 需要负责释放 CameraColor 时才添加释放 Pass。
            {
                graph.AddPass(releaseCameraColorPass); // 添加 CameraColor 释放 Pass。
            }

            if (renderOptions.ShouldReleaseCameraDepth) // 当前 request 需要负责释放 CameraDepth 时才添加释放 Pass。
            {
                graph.AddPass(releaseCameraDepthPass); // 添加 CameraDepth 释放 Pass。
            }
        }

        private static bool ShouldUseLocalGBufferTargets(BurtRequestRenderOptions renderOptions) // 判断当前 request 是否应该拥有本地图内 GBuffer 生命周期。
        {
            if (renderOptions == null) // renderOptions 为空时按单 request 兜底处理。
            {
                return true; // 返回 true，让旧入口仍然申请和释放 GBuffer。
            }

            return renderOptions.ShouldAllocateCameraColor && renderOptions.ShouldAllocateCameraDepth; // 当前阶段只在申请 CameraColor/CameraDepth 的 request 上申请 GBuffer。
        }

        private static bool ShouldUseHiZDepth(
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset,
            bool useLocalGBufferTargets)
        {
            return useLocalGBufferTargets &&
                (BurtHiZDepthPassUtility.ShouldUseHiZDepth(request, asset) ||
                    BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(request, asset));
        }

        private static bool ShouldSeedOverlayCameraColor( // 判断 Overlay 是否需要从最终目标继承颜色。
            BurtRenderRequest request, // 接收当前 request，用来判断相机角色和清颜色意图。
            BurtRequestRenderOptions renderOptions) // 接收相机栈 RT 生命周期选项，用来判断是否共享 CameraColor。
        {
            if (request == null) // request 为空说明输入异常。
            {
                return false; // 返回 false，避免异常 request 添加 Overlay 继承 Pass。
            }

            if (renderOptions != null && renderOptions.UseSharedRenderTargets) // 共享栈级 RT 时，Base 的结果已经留在 CameraColor 里。
            {
                return false; // 返回 false，避免从尚未 FinalBlit 的最终目标复制旧画面。
            }

            return request.Type == BurtRenderRequestType.OverlayCamera && !request.OverlayClearsColor; // 非共享 Overlay 且不清颜色时才需要复制最终目标作为底图。
        }

        private static bool ShouldUseDepthPrepass(BurtRenderPipelineAsset asset) // 判断是否启用 Depth Prepass。
        {
            if (asset == null) // asset 为空时没有 Inspector 配置来源。
            {
                return true; // 默认启用 Depth Prepass，保持教程管线的安全行为。
            }

            return asset.EnableDepthPrepass; // 使用资产上的 Depth Prepass 开关。
        }

        private static bool ShouldUseDeferredForwardOnlyOpaqueFallback(BurtRenderPipelineAsset asset) // 判断 Deferred 模式是否启用 ForwardOnly 不透明兜底。
        {
            if (asset == null) // asset 为空时没有 Inspector 配置来源。
            {
                return true; // 默认开启兜底入口，方便显式声明 BurtForwardOnly 的 shader 在异常资产路径下仍有绘制机会。
            }

            return asset.EnableDeferredForwardOpaqueFallback; // 使用资产上的 Deferred ForwardOnly 不透明兜底开关。
        }

        private const int OpaqueRenderQueueMax = 2500;
        private const string ClearCoatShaderName = "BurtRP/Clear Coat";
        private const string GBufferPassName = "BurtGBuffer";
        private static int cachedClearCoatFeatureFrame = -1;
        private static int cachedClearCoatFeatureCameraId;
        private static int cachedClearCoatFeatureCullingMask;
        private static bool cachedHasVisibleClearCoat;

        private static bool ShouldUseDeferredClearCoatLighting(BurtRenderRequest request, BurtRenderPipelineAsset asset, bool useLocalGBufferTargets)
        {
            if (!useLocalGBufferTargets)
            {
                return false;
            }

            if (asset != null && asset.RendererMode != BurtRendererMode.Deferred)
            {
                return false;
            }

            var camera = request != null ? request.Camera : null;
            if (request == null || !request.IsValid || camera == null)
            {
                return true;
            }

            var frame = UnityEngine.Time.frameCount;
            var cameraId = camera.GetInstanceID();
            if (cachedClearCoatFeatureFrame == frame &&
                cachedClearCoatFeatureCameraId == cameraId &&
                cachedClearCoatFeatureCullingMask == camera.cullingMask)
            {
                return cachedHasVisibleClearCoat;
            }

            cachedClearCoatFeatureFrame = frame;
            cachedClearCoatFeatureCameraId = cameraId;
            cachedClearCoatFeatureCullingMask = camera.cullingMask;
            cachedHasVisibleClearCoat = TryScanVisibleOpaqueClearCoatMaterial(camera, out var hasVisibleClearCoat)
                ? hasVisibleClearCoat
                : true;

            return cachedHasVisibleClearCoat;
        }

        private static bool TryScanVisibleOpaqueClearCoatMaterial(UnityEngine.Camera camera, out bool hasVisibleClearCoat)
        {
            hasVisibleClearCoat = false;
            UnityEngine.Renderer[] renderers;
            UnityEngine.Plane[] frustumPlanes;
            try
            {
                renderers = FindActiveRenderers();
                frustumPlanes = UnityEngine.GeometryUtility.CalculateFrustumPlanes(camera);
            }
            catch (System.Exception)
            {
                return false;
            }

            if (renderers == null)
            {
                return true;
            }

            for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                var renderer = renderers[rendererIndex];
                if (!IsActiveRenderer(renderer) ||
                    !IsRendererInCameraLayer(renderer, camera) ||
                    !IsRendererInFrustum(renderer, frustumPlanes))
                {
                    continue;
                }

                if (RendererHasOpaqueClearCoatMaterial(renderer))
                {
                    hasVisibleClearCoat = true;
                    return true;
                }
            }

            return true;
        }

        private static UnityEngine.Renderer[] FindActiveRenderers()
        {
#if UNITY_2022_2_OR_NEWER
            return UnityEngine.Object.FindObjectsByType<UnityEngine.Renderer>(UnityEngine.FindObjectsSortMode.None);
#else
            return UnityEngine.Object.FindObjectsOfType<UnityEngine.Renderer>();
#endif
        }

        private static bool IsActiveRenderer(UnityEngine.Renderer renderer)
        {
            return renderer != null &&
                renderer.enabled &&
                renderer.gameObject != null &&
                renderer.gameObject.activeInHierarchy;
        }

        private static bool IsRendererInCameraLayer(UnityEngine.Renderer renderer, UnityEngine.Camera camera)
        {
            var gameObject = renderer != null ? renderer.gameObject : null;
            return gameObject != null && camera != null && (camera.cullingMask & (1 << gameObject.layer)) != 0;
        }

        private static bool IsRendererInFrustum(UnityEngine.Renderer renderer, UnityEngine.Plane[] frustumPlanes)
        {
            if (frustumPlanes == null || frustumPlanes.Length == 0)
            {
                return true;
            }

            return renderer != null && UnityEngine.GeometryUtility.TestPlanesAABB(frustumPlanes, renderer.bounds);
        }

        private static bool RendererHasOpaqueClearCoatMaterial(UnityEngine.Renderer renderer)
        {
            var materials = renderer != null ? renderer.sharedMaterials : null;
            if (materials == null)
            {
                return false;
            }

            for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                if (IsOpaqueClearCoatGBufferMaterial(materials[materialIndex]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsOpaqueClearCoatGBufferMaterial(UnityEngine.Material material)
        {
            return material != null &&
                material.shader != null &&
                material.shader.name == ClearCoatShaderName &&
                IsOpaqueMaterial(material) &&
                material.GetShaderPassEnabled(GBufferPassName);
        }

        private static bool IsOpaqueMaterial(UnityEngine.Material material)
        {
            var renderQueue = material.renderQueue;
            if (renderQueue >= 0)
            {
                return renderQueue <= OpaqueRenderQueueMax;
            }

            var queueTag = material.GetTag("Queue", true, "Geometry");
            return !queueTag.StartsWith("Transparent", System.StringComparison.OrdinalIgnoreCase) &&
                !queueTag.StartsWith("Overlay", System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldUseUnsupportedShaderDebug(BurtRenderPipelineAsset asset) // 判断是否插入不支持 Shader 调试 Pass。
        {
            if (asset == null) // asset 为空时没有 Inspector 配置来源。
            {
                return true; // 默认开启错误材质显示，避免不支持材质静默消失。
            }

            return asset.EnableUnsupportedShaderDebug; // 使用资产上的不支持 Shader 调试开关。
        }

        private static bool ShouldUsePostProcessFramework( // 判断是否启用后处理链路。
            BurtRenderRequest request, // 接收当前 request。
            BurtRenderPipelineAsset asset, // 接收管线资产配置。
            BurtRequestRenderOptions renderOptions) // 接收相机栈 RT 生命周期选项。
        {
            if (renderOptions != null && !renderOptions.ShouldFinalBlit) // 当前 request 不是最终输出点时不应该执行后处理。
            {
                return false; // 返回 false，把后处理推迟到真正 FinalBlit 之前。
            }

            return BurtPostProcessUtility.ShouldUsePostProcessFramework(request, asset); // 复用后处理工具逻辑，保证资源注册和 Pass 组装条件一致。
        }

        private static bool ShouldUseDepthDebugView(BurtRenderPipelineAsset asset) // 判断是否启用 CameraDepth 调试视图。
        {
            if (asset == null) // asset 为空时没有 Inspector 配置来源。
            {
                return false; // 默认关闭调试视图，避免覆盖正常画面。
            }

            return asset.EnableDepthDebugView; // 使用资产上的深度调试开关。
        }

        private static bool ShouldUseMainLightShadowDebugView( // 判断是否启用主光 shadow map 调试视图。
            BurtRenderPipelineAsset asset, // 接收管线资产配置。
            bool useMainLightShadow) // 接收当前 request 是否真的生成了主光阴影图。
        {
            if (!useMainLightShadow) // 当前 request 没有 shadow map 时没有可视化目标。
            {
                return false; // 返回 false，避免 debug pass 读取无效资源。
            }

            if (asset == null) // asset 为空时没有 Inspector 配置来源。
            {
                return false; // 默认关闭 shadow map 调试视图。
            }

            return BurtShadingDebugSettings.Mode == BurtShadingDebugMode.MainLightShadow; // Controlled by Shading Debug Overlay instead of Pipeline Asset.
        }

        private static bool ShouldUseGBufferDebugView( // 判断是否启用 Deferred GBuffer 调试视图。
            BurtRenderPipelineAsset asset, // 接收管线资产配置。
            bool useLocalGBufferTargets) // 接收当前 request 是否真的申请了 GBuffer。
        {
            return BurtGBufferDebugViewUtility.ShouldUseGBufferDebugView(asset, useLocalGBufferTargets); // 统一使用资产面板和 Shading Debug Overlay 的合并结果决定是否插入 GBuffer Debug Pass。
        }

        private static bool ShouldUseHiZDebugView(
            BurtRenderPipelineAsset asset,
            bool useLocalGBufferTargets)
        {
            return useLocalGBufferTargets && BurtDebugHiZDepthPass.ShouldUseHiZDebugView(asset);
        }

        private static bool ShouldUseScreenSpaceAmbientOcclusionDebugView(
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset,
            bool useLocalGBufferTargets)
        {
            return useLocalGBufferTargets && BurtScreenSpaceAmbientOcclusionPassUtility.ShouldUseScreenSpaceAmbientOcclusionDebugView(request, asset);
        }

        private static bool ShouldUseScreenSpaceSubsurfaceDebugView(
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset,
            bool useLocalGBufferTargets)
        {
            return useLocalGBufferTargets && BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceDebugView(request, asset);
        }

        private static bool ShouldUseScreenSpaceGlobalIlluminationDebugView(
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset,
            bool useLocalGBufferTargets)
        {
            return useLocalGBufferTargets && BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIlluminationDebugView(request, asset);
        }

        private static bool ShouldUseScreenSpaceReflectionHiZDiagnosticView(
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset,
            bool useLocalGBufferTargets)
        {
            return useLocalGBufferTargets && BurtScreenSpaceReflectionPassUtility.ShouldUseScreenSpaceReflectionHiZDiagnosticView(request, asset);
        }
    }
}
