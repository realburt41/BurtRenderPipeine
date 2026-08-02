using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine.Rendering; // 引入 Unity 渲染命名空间，用来使用 ScriptableRenderContext。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让这个上下文类和其他 BurtRP 代码处在同一个模块里。
{
    public sealed class BurtRenderGraphContext // 定义 RenderGraph 执行上下文，用来打包一次图执行需要的公共数据和资源表。
    {
        private const int MaxPooledContextCount = 8;
        private static readonly Stack<BurtRenderGraphContext> ContextPool = new Stack<BurtRenderGraphContext>();
        private readonly List<ProfilingSampler> activeProfilingSamplers = new List<ProfilingSampler>();
        private readonly List<ProfilingSampler> submittedProfilingSamplers = new List<ProfilingSampler>();

        public ScriptableRenderContext ScriptableContext { get; private set; } // 保存 Unity SRP 的渲染上下文，Pass 通过它提交绘制命令。

        private CommandBuffer graphCommandBuffer;
        private bool commandBufferHasCommands;
        private bool graphCommandBufferHasCommands;
        // Profiling markers are commands too, but marker-only buffers should not become empty
        // physical submissions in RenderDoc.
        private bool commandBufferHasWork;
        private bool graphCommandBufferHasWork;
        // True while the current graphics/compute queue owns the logical profiling stack. The
        // stack may remain open after its source CommandBuffer has been executed and cleared.
        private bool profilingScopesBoundToCurrentQueue;

        public CommandBuffer CommandBuffer { get; private set; } // Current graph or async-pass command buffer used by migrated passes and profiling markers.

        public bool HasSharedCommandBuffer => CommandBuffer != null;

        public BurtRenderRequest Request { get; private set; } // 保存当前正在执行的渲染请求，Pass 通过它读取 Camera、CullingResults 等任务数据。

        public BurtRenderPipelineAsset Asset { get; private set; } // 保存当前管线资产，Pass 通过它读取默认清屏色等全局配置。

        public BurtRenderGraphResourceRegistry ResourceRegistry { get; private set; } // 保存当前 RenderGraph 的资源注册表，Pass 通过它读取图资源。

        public BurtRenderTargetHandle CameraColorTarget // 定义读取 CameraColor 的快捷属性，方便 Pass 不直接操作资源名。
        {
            get // 定义属性 getter，每次访问时从资源注册表读取最新的 CameraColor。
            {
                if (ResourceRegistry == null) // 如果资源注册表为空，说明当前上下文没有可用资源表。
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraColorName); // 返回无效 CameraColor 句柄，避免 Pass 绑定错误目标。
                }

                return ResourceRegistry.GetCameraColor(); // 从资源注册表读取 CameraColor 句柄。
            }
        }

        public BurtRenderTargetHandle OpaqueCameraColorTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.OpaqueCameraColorName);
                }

                return ResourceRegistry.GetOpaqueCameraColor();
            }
        }

        public BurtRenderTargetHandle RefractionDistortionTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.RefractionDistortionName);
                }

                return ResourceRegistry.GetRefractionDistortion();
            }
        }

        public BurtRenderTargetHandle RefractionSceneColorMipChainTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.RefractionSceneColorMipChainName);
                }

                return ResourceRegistry.GetRefractionSceneColorMipChain();
            }
        }

        public BurtRenderTargetHandle FinalCameraTarget // 定义读取最终相机输出目标的快捷属性，方便 FinalBlit 不直接操作资源名。
        {
            get // 定义属性 getter，每次访问时从资源注册表读取最新的 FinalCameraTarget。
            {
                if (ResourceRegistry == null) // 如果资源注册表为空，说明当前上下文没有可用资源表。
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FinalCameraTargetName); // 返回无效最终目标句柄，避免 FinalBlit 绑定错误输出。
                }

                return ResourceRegistry.GetFinalCameraTarget(); // 从资源注册表读取最终相机输出目标句柄。
            }
        }

        public BurtRenderTargetHandle CameraDepthTarget // 定义读取 CameraDepth 的快捷属性，方便 Pass 不直接操作资源名。
        {
            get // 定义属性 getter，每次访问时从资源注册表读取最新的 CameraDepth。
            {
                if (ResourceRegistry == null) // 如果资源注册表为空，说明当前上下文没有可用资源表。
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName); // 返回无效 CameraDepth 句柄，避免 Pass 绑定错误目标。
                }

                return ResourceRegistry.GetCameraDepth(); // 从资源注册表读取 CameraDepth 句柄。
            }
        }

        public BurtRenderTargetHandle DeferredLightingDepthTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.DeferredLightingDepthName);
                }

                return ResourceRegistry.GetDeferredLightingDepth();
            }
        }

        public BurtRenderTargetHandle PostProcessColorTarget // 定义读取 PostProcessColor 的快捷属性，方便后处理 Pass 不直接操作资源名。
        {
            get // 定义属性 getter，每次访问时从资源注册表读取最新的 PostProcessColor。
            {
                if (ResourceRegistry == null) // 如果资源注册表为空，说明当前上下文没有可用资源表。
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.PostProcessColorName); // 返回无效 PostProcessColor 句柄，避免 Pass 绑定错误目标。
                }

                return ResourceRegistry.GetPostProcessColor(); // 从资源注册表读取 PostProcessColor 句柄。
            }
        }

        public BurtRenderTargetHandle TemporalAAOutputTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.TemporalAAOutputName);
                }

                return ResourceRegistry.GetTemporalAAOutput();
            }
        }

        public BurtRenderTargetHandle MainLightShadowMapTarget // 定义读取 MainLightShadowMap 的快捷属性，方便 Pass 不直接操作资源名。
        {
            get // 定义属性 getter，每次访问时从资源注册表读取最新的 MainLightShadowMap。
            {
                if (ResourceRegistry == null) // 如果资源注册表为空，说明当前上下文没有可用资源表。
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.MainLightShadowMapName); // 返回无效 MainLightShadowMap 句柄，避免 Pass 绑定错误阴影目标。
                }

                return ResourceRegistry.GetMainLightShadowMap(); // 从资源注册表读取 MainLightShadowMap 句柄。
            }
        }

        public BurtRenderTargetHandle GBuffer0Target // 定义读取 GBuffer0 的快捷属性，方便 Deferred Pass 不直接操作资源名。
        {
            get // 定义属性 getter，每次访问时从资源注册表读取最新的 GBuffer0。
            {
                if (ResourceRegistry == null) // 如果资源注册表为空，说明当前上下文没有可用资源表。
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer0Name); // 返回无效 GBuffer0 句柄，避免 Deferred Pass 绑定错误目标。
                }

                return ResourceRegistry.GetGBuffer0(); // 从资源注册表读取 GBuffer0 句柄。
            }
        }

        public BurtRenderTargetHandle GBuffer1Target // 定义读取 GBuffer1 的快捷属性，方便 Deferred Pass 不直接操作资源名。
        {
            get // 定义属性 getter，每次访问时从资源注册表读取最新的 GBuffer1。
            {
                if (ResourceRegistry == null) // 如果资源注册表为空，说明当前上下文没有可用资源表。
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer1Name); // 返回无效 GBuffer1 句柄，避免 Deferred Pass 绑定错误目标。
                }

                return ResourceRegistry.GetGBuffer1(); // 从资源注册表读取 GBuffer1 句柄。
            }
        }

        public BurtRenderTargetHandle GBuffer2Target // 定义读取 GBuffer2 的快捷属性，方便 Deferred Pass 不直接操作资源名。
        {
            get // 定义属性 getter，每次访问时从资源注册表读取最新的 GBuffer2。
            {
                if (ResourceRegistry == null) // 如果资源注册表为空，说明当前上下文没有可用资源表。
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer2Name); // 返回无效 GBuffer2 句柄，避免 Deferred Pass 绑定错误目标。
                }

                return ResourceRegistry.GetGBuffer2(); // 从资源注册表读取 GBuffer2 句柄。
            }
        }

        public BurtRenderTargetHandle GBuffer3Target
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer3Name);
                }

                return ResourceRegistry.GetGBuffer3();
            }
        }

        public BurtRenderTargetHandle GBuffer4Target
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer4Name);
                }

                return ResourceRegistry.GetGBuffer4();
            }
        }

        public BurtRenderTargetHandle GBuffer5Target
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer5Name);
                }

                return ResourceRegistry.GetGBuffer5();
            }
        }

        public BurtRenderTargetHandle GBufferObjectIndexTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBufferObjectIndexName);
                }

                return ResourceRegistry.GetGBufferObjectIndex();
            }
        }

        public BurtRenderTargetHandle HiZDepthTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.HiZDepthName);
                }

                return ResourceRegistry.GetHiZDepth();
            }
        }

        public BurtRenderTargetHandle ScreenSpaceReflectionColorTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceReflectionColorName);
                }

                return ResourceRegistry.GetScreenSpaceReflectionColor();
            }
        }

        public BurtRenderTargetHandle ScreenSpaceReflectionDenoisedColorTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceReflectionDenoisedColorName);
                }

                return ResourceRegistry.GetScreenSpaceReflectionDenoisedColor();
            }
        }

        public BurtRenderTargetHandle ScreenSpaceReflectionTemporalColorTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceReflectionTemporalColorName);
                }

                return ResourceRegistry.GetScreenSpaceReflectionTemporalColor();
            }
        }

        public BurtRenderTargetHandle ScreenSpaceAmbientOcclusionRawTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceAmbientOcclusionRawName);
                }

                return ResourceRegistry.GetScreenSpaceAmbientOcclusionRaw();
            }
        }

        public BurtRenderTargetHandle ScreenSpaceAmbientOcclusionTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceAmbientOcclusionName);
                }

                return ResourceRegistry.GetScreenSpaceAmbientOcclusion();
            }
        }

        public BurtRenderTargetHandle ScreenSpaceShadowTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceShadowName);
                }

                return ResourceRegistry.GetScreenSpaceShadow();
            }
        }

        public BurtRenderTargetHandle ScreenSpaceGlobalIlluminationRawTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceGlobalIlluminationRawName);
                }

                return ResourceRegistry.GetScreenSpaceGlobalIlluminationRaw();
            }
        }

        public BurtRenderTargetHandle ScreenSpaceGlobalIlluminationTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceGlobalIlluminationName);
                }

                return ResourceRegistry.GetScreenSpaceGlobalIllumination();
            }
        }

        public BurtRenderTargetHandle ScreenSpaceGlobalIlluminationUpsampledTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceGlobalIlluminationUpsampledName);
                }

                return ResourceRegistry.GetScreenSpaceGlobalIlluminationUpsampled();
            }
        }

        public BurtRenderTargetHandle BurtGITemporalDiagnosticsTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGITemporalDiagnosticsName);
                }

                return ResourceRegistry.GetBurtGITemporalDiagnostics();
            }
        }

        public BurtRenderTargetHandle BurtGIRadianceCacheStatsTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheStatsName);
                }

                return ResourceRegistry.GetBurtGIRadianceCacheStats();
            }
        }

        public BurtRenderTargetHandle BurtGIBackfaceDiffuseIndirectTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIBackfaceDiffuseIndirectName);
                }

                return ResourceRegistry.GetBurtGIBackfaceDiffuseIndirect();
            }
        }

        public BurtRenderTargetHandle BurtGIBackfaceDiffuseIndirectUpsampledTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIBackfaceDiffuseIndirectUpsampledName);
                }

                return ResourceRegistry.GetBurtGIBackfaceDiffuseIndirectUpsampled();
            }
        }

        public BurtRenderTargetHandle BurtGIRoughSpecularIndirectTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIRoughSpecularIndirectName);
                }

                return ResourceRegistry.GetBurtGIRoughSpecularIndirect();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeIntegrateTileClassificationTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeIntegrateTileClassificationName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeIntegrateTileClassification();
            }
        }

        public BurtRenderTargetHandle BurtGIRoughSpecularIndirectUpsampledTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIRoughSpecularIndirectUpsampledName);
                }

                return ResourceRegistry.GetBurtGIRoughSpecularIndirectUpsampled();
            }
        }

        public BurtRenderTargetHandle BurtGITranslucencyVolume0Target
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGITranslucencyVolume0Name);
                }

                return ResourceRegistry.GetBurtGITranslucencyVolume0();
            }
        }

        public BurtRenderTargetHandle BurtGITranslucencyVolume1Target
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGITranslucencyVolume1Name);
                }

                return ResourceRegistry.GetBurtGITranslucencyVolume1();
            }
        }

        public BurtRenderTargetHandle BurtGITranslucencyVolumeFilter0Target
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGITranslucencyVolumeFilter0Name);
                }

                return ResourceRegistry.GetBurtGITranslucencyVolumeFilter0();
            }
        }

        public BurtRenderTargetHandle BurtGITranslucencyVolumeFilter1Target
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGITranslucencyVolumeFilter1Name);
                }

                return ResourceRegistry.GetBurtGITranslucencyVolumeFilter1();
            }
        }

        public BurtRenderTargetHandle BurtGITranslucencyVolumeTraceRadianceTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGITranslucencyVolumeTraceRadianceName);
                }

                return ResourceRegistry.GetBurtGITranslucencyVolumeTraceRadiance();
            }
        }

        public BurtRenderTargetHandle BurtGITranslucencyVolumeTraceFilteredRadianceTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGITranslucencyVolumeTraceFilteredRadianceName);
                }

                return ResourceRegistry.GetBurtGITranslucencyVolumeTraceFilteredRadiance();
            }
        }

        public BurtRenderTargetHandle BurtGITranslucencyVolumeTraceHitDistanceTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGITranslucencyVolumeTraceHitDistanceName);
                }

                return ResourceRegistry.GetBurtGITranslucencyVolumeTraceHitDistance();
            }
        }

        public BurtRenderTargetHandle BurtGISceneVoxelRadianceTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGISceneVoxelRadianceName);
                }

                return ResourceRegistry.GetBurtGISceneVoxelRadiance();
            }
        }

        public BurtRenderTargetHandle BurtGISceneVoxelGeometryTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGISceneVoxelGeometryName);
                }

                return ResourceRegistry.GetBurtGISceneVoxelGeometry();
            }
        }

        public BurtRenderTargetHandle BurtGISceneVoxelOccupancyMipTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGISceneVoxelOccupancyMipName);
                }

                return ResourceRegistry.GetBurtGISceneVoxelOccupancyMip();
            }
        }

        public BurtRenderTargetHandle BurtGISceneVoxelLightingTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGISceneVoxelLightingName);
                }

                return ResourceRegistry.GetBurtGISceneVoxelLighting();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeScreenDepthTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeScreenDepthName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeScreenDepth();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeWorldNormalTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeWorldNormalName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeWorldNormal();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeWorldPositionTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeWorldPositionName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeWorldPosition();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeRadianceTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeRadianceName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeRadiance();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeAdaptiveProbeHeaderTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeAdaptiveProbeHeaderName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeAdaptiveProbeHeader();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeAdaptiveProbeIndicesTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeAdaptiveProbeIndicesName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeAdaptiveProbeIndices();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeIrradianceTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeIrradianceName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeIrradiance();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeConfidenceTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeConfidenceName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeConfidence();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeHitDistanceTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeHitDistanceName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeHitDistance();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeBentNormalTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeBentNormalName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeBentNormal();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeTraceRadianceTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceRadianceName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeTraceRadiance();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeTraceHitTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceHitName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeTraceHit();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeTemporalRadianceTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeTemporalRadianceName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeTemporalRadiance();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeTemporalIrradianceTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeTemporalIrradianceName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeTemporalIrradiance();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeTemporalConfidenceTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeTemporalConfidenceName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeTemporalConfidence();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeFilteredRadianceTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeFilteredRadianceName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeFilteredRadiance();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeFilteredIrradianceTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeFilteredIrradianceName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeFilteredIrradiance();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeFilteredConfidenceTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeFilteredConfidenceName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeFilteredConfidence();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeFixupRadianceTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeFixupRadianceName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeFixupRadiance();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeFixupIrradianceTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeFixupIrradianceName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeFixupIrradiance();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeFixupConfidenceTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeFixupConfidenceName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeFixupConfidence();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeMipRadianceTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeMipRadianceName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeMipRadiance();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeMipIrradianceTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeMipIrradianceName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeMipIrradiance();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeMipConfidenceTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeMipConfidenceName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeMipConfidence();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeMip2RadianceTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeMip2RadianceName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeMip2Radiance();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeMip2IrradianceTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeMip2IrradianceName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeMip2Irradiance();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeMip2ConfidenceTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeMip2ConfidenceName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeMip2Confidence();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeMip3RadianceTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeMip3RadianceName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeMip3Radiance();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeMip3IrradianceTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeMip3IrradianceName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeMip3Irradiance();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeMip3ConfidenceTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeMip3ConfidenceName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeMip3Confidence();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeRadianceSHAmbientTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeRadianceSHAmbientName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeRadianceSHAmbient();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeRadianceSHDirectionalTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeRadianceSHDirectionalName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeRadianceSHDirectional();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeIrradianceOctTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeIrradianceOctName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeIrradianceOct();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeRadianceOctTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeRadianceOctName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeRadianceOct();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeImportancePDFTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeImportancePDFName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeImportancePDF();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeImportanceLightPDFTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeImportanceLightPDFName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeImportanceLightPDF();
            }
        }

        public BurtRenderTargetHandle BurtGIScreenProbeImportanceRayInfoTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIScreenProbeImportanceRayInfoName);
                }

                return ResourceRegistry.GetBurtGIScreenProbeImportanceRayInfo();
            }
        }

        public BurtRenderTargetHandle BurtGIRadianceCacheClipMapIndirectionTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapIndirectionName);
                }

                return ResourceRegistry.GetBurtGIRadianceCacheClipMapIndirection();
            }
        }

        public BurtRenderTargetHandle BurtGIRadianceCacheClipMapDepthProbeAtlasTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapDepthProbeAtlasName);
                }

                return ResourceRegistry.GetBurtGIRadianceCacheClipMapDepthProbeAtlas();
            }
        }

        public BurtRenderTargetHandle BurtGIRadianceCacheClipMapRadianceProbeAtlasTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapRadianceProbeAtlasName);
                }

                return ResourceRegistry.GetBurtGIRadianceCacheClipMapRadianceProbeAtlas();
            }
        }

        public BurtRenderTargetHandle BurtGIRadianceCacheClipMapFinalRadianceAtlasTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapFinalRadianceAtlasName);
                }

                return ResourceRegistry.GetBurtGIRadianceCacheClipMapFinalRadianceAtlas();
            }
        }

        public BurtRenderTargetHandle BurtGIRadianceCacheClipMapFinalIrradianceAtlasTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapFinalIrradianceAtlasName);
                }

                return ResourceRegistry.GetBurtGIRadianceCacheClipMapFinalIrradianceAtlas();
            }
        }

        public BurtRenderTargetHandle BurtGIRadianceCacheClipMapProbeOcclusionAtlasTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeOcclusionAtlasName);
                }

                return ResourceRegistry.GetBurtGIRadianceCacheClipMapProbeOcclusionAtlas();
            }
        }

        public BurtRenderTargetHandle BurtGIRadianceCacheClipMapProbeSkyAOAtlasTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeSkyAOAtlasName);
                }

                return ResourceRegistry.GetBurtGIRadianceCacheClipMapProbeSkyAOAtlas();
            }
        }

        public BurtRenderTargetHandle ScreenSpaceSubsurfaceSourceTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSourceName);
                }

                return ResourceRegistry.GetScreenSpaceSubsurfaceSource();
            }
        }

        public BurtRenderTargetHandle ScreenSpaceSubsurfaceBaseColorTarget
        {
            get { return GBuffer1Target; }
        }

        public BurtRenderTargetHandle ScreenSpaceSubsurfaceEmissionTarget
        {
            get { return GBuffer4Target; }
        }

        public BurtRenderTargetHandle ScreenSpaceSubsurfaceSetupTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSetupName);
                }

                return ResourceRegistry.GetScreenSpaceSubsurfaceSetup();
            }
        }

        public BurtRenderTargetHandle ScreenSpaceSubsurfaceProfileIDAndTypeTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceProfileIDAndTypeName);
                }

                return ResourceRegistry.GetScreenSpaceSubsurfaceProfileIDAndType();
            }
        }

        public BurtRenderTargetHandle ScreenSpaceSubsurfaceMaskTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceMaskName);
                }

                return ResourceRegistry.GetScreenSpaceSubsurfaceMask();
            }
        }

        public BurtRenderTargetHandle ScreenSpaceSubsurfaceTempTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceTempName);
                }

                return ResourceRegistry.GetScreenSpaceSubsurfaceTemp();
            }
        }

        public BurtRenderTargetHandle ScreenSpaceSubsurfaceBlurTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBlurName);
                }

                return ResourceRegistry.GetScreenSpaceSubsurfaceBlur();
            }
        }

        public BurtRenderTargetHandle ScreenSpaceSubsurfaceCombineTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceCombineName);
                }

                return ResourceRegistry.GetScreenSpaceSubsurfaceCombine();
            }
        }

        public BurtRenderTargetHandle ScreenSpaceSubsurfaceVelocityTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceVelocityName);
                }

                return ResourceRegistry.GetScreenSpaceSubsurfaceVelocity();
            }
        }

        public BurtRenderTargetHandle FurBlurPropertyTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurPropertyName);
                }

                return ResourceRegistry.GetFurBlurProperty();
            }
        }

        public BurtRenderTargetHandle FurBlurPropertyTempTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurPropertyTempName);
                }

                return ResourceRegistry.GetFurBlurPropertyTemp();
            }
        }

        public BurtRenderTargetHandle FurBlurColorTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurColorName);
                }

                return ResourceRegistry.GetFurBlurColor();
            }
        }

        public BurtRenderTargetHandle FurBlurTemporalTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurTemporalName);
                }

                return ResourceRegistry.GetFurBlurTemporal();
            }
        }

        public BurtRenderTargetHandle FurBlurVelocityTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurVelocityName);
                }

                return ResourceRegistry.GetFurBlurVelocity();
            }
        }

        public BurtRenderBufferHandle GetBuffer(string name)
        {
            if (ResourceRegistry == null)
            {
                return BurtRenderBufferHandle.Invalid(name);
            }

            return ResourceRegistry.GetBuffer(name);
        }

        public BurtRenderTargetHandle AdditionalLightShadowAtlasTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.AdditionalLightShadowAtlasName);
                }

                return ResourceRegistry.GetAdditionalLightShadowAtlas();
            }
        }

        public BurtRenderTargetHandle PerObjectShadowAtlasTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.PerObjectShadowAtlasName);
                }

                return ResourceRegistry.GetPerObjectShadowAtlas();
            }
        }

        public BurtRenderBufferHandle AdditionalLightBuffer => GetBuffer(BurtRenderGraphResourceRegistry.AdditionalLightBufferName);

        public BurtRenderBufferHandle TileLightCountBuffer => GetBuffer(BurtRenderGraphResourceRegistry.TileLightCountBufferName);

        public BurtRenderBufferHandle TileLightListBuffer => GetBuffer(BurtRenderGraphResourceRegistry.TileLightListBufferName);

        public BurtRenderBufferHandle TileLightOffsetBuffer => GetBuffer(BurtRenderGraphResourceRegistry.TileLightOffsetBufferName);

        public BurtRenderBufferHandle ClusterLightCountBuffer => GetBuffer(BurtRenderGraphResourceRegistry.ClusterLightCountBufferName);

        public BurtRenderBufferHandle ClusterLightListBuffer => GetBuffer(BurtRenderGraphResourceRegistry.ClusterLightListBufferName);

        public BurtRenderBufferHandle ClusterLightOffsetBuffer => GetBuffer(BurtRenderGraphResourceRegistry.ClusterLightOffsetBufferName);

        public BurtRenderBufferHandle PunctualTileIdBuffer => GetBuffer(BurtRenderGraphResourceRegistry.PunctualTileIdBufferName);

        public BurtRenderBufferHandle ScreenSpaceSubsurfaceBurleyArgsBuffer => GetBuffer(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBurleyArgsBufferName);

        public BurtRenderBufferHandle ScreenSpaceSubsurfaceBurleyGroupBuffer => GetBuffer(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBurleyGroupBufferName);

        public BurtRenderBufferHandle ScreenSpaceSubsurfaceSeparableArgsBuffer => GetBuffer(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSeparableArgsBufferName);

        public BurtRenderBufferHandle ScreenSpaceSubsurfaceSeparableGroupBuffer => GetBuffer(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSeparableGroupBufferName);

        public BurtRenderBufferHandle FurBlurArgsBuffer => GetBuffer(BurtRenderGraphResourceRegistry.FurBlurArgsBufferName);

        public BurtRenderBufferHandle FurBlurTileDataBuffer => GetBuffer(BurtRenderGraphResourceRegistry.FurBlurTileDataBufferName);

        public BurtRenderBufferHandle BurtGIScreenProbeIndirectArgsBuffer => GetBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeIndirectArgsBufferName);

        public BurtRenderBufferHandle BurtGIScreenProbeIntegrateTileIndirectArgsBuffer => GetBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeIntegrateTileIndirectArgsBufferName);

        public BurtRenderBufferHandle BurtGIScreenProbeIntegrateTileDataDiffuseBuffer => GetBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeIntegrateTileDataDiffuseBufferName);

        public BurtRenderBufferHandle BurtGIScreenProbeIntegrateTileDataAllBuffer => GetBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeIntegrateTileDataAllBufferName);

        public BurtRenderBufferHandle BurtGIScreenProbeTraceCompactTexelCountBuffer => GetBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceCompactTexelCountBufferName);

        public BurtRenderBufferHandle BurtGIScreenProbeTraceCompactTexelDataBuffer => GetBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceCompactTexelDataBufferName);

        public BurtRenderBufferHandle BurtGIScreenProbeTraceCompactIndirectArgsBuffer => GetBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceCompactIndirectArgsBufferName);

        public BurtRenderBufferHandle BurtGIScreenProbeTraceCompactThreadCountXBuffer => GetBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceCompactThreadCountXBufferName);

        public BurtRenderBufferHandle BurtGIScreenProbeAdaptiveProbeNumBuffer => GetBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeAdaptiveProbeNumBufferName);

        public BurtRenderBufferHandle BurtGIScreenProbeAdaptiveProbeDataBuffer => GetBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeAdaptiveProbeDataBufferName);

        public BurtRenderBufferHandle BurtGIScreenProbeImportancePDFSHBuffer => GetBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeImportancePDFSHBufferName);

        public BurtRenderBufferHandle BurtGIRadianceCacheClipMapProbeAllocatorBuffer => GetBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeAllocatorBufferName);

        public BurtRenderBufferHandle BurtGIRadianceCacheClipMapProbeFreeListAllocatorBuffer => GetBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeFreeListAllocatorBufferName);

        public BurtRenderBufferHandle BurtGIRadianceCacheClipMapProbeFreeListBuffer => GetBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeFreeListBufferName);

        public BurtRenderBufferHandle BurtGIRadianceCacheClipMapProbeLastUsedFrameBuffer => GetBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeLastUsedFrameBufferName);

        public BurtRenderBufferHandle BurtGIRadianceCacheClipMapProbeLastTracedFrameBuffer => GetBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeLastTracedFrameBufferName);

        public BurtRenderBufferHandle BurtGIRadianceCacheClipMapProbeWorldOffsetBuffer => GetBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeWorldOffsetBufferName);

        public BurtRenderBufferHandle BurtGIRadianceCacheClipMapProbeTraceDataBuffer => GetBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeTraceDataBufferName);

        public BurtRenderBufferHandle BurtGIRadianceCacheClipMapProbeTraceAllocatorBuffer => GetBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeTraceAllocatorBufferName);

        public BurtRenderBufferHandle BurtGIRadianceCacheClipMapPriorityHistogramBuffer => GetBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapPriorityHistogramBufferName);

        public BurtRenderBufferHandle BurtGIRadianceCacheClipMapMaxUpdateBucketBuffer => GetBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapMaxUpdateBucketBufferName);

        public BurtRenderBufferHandle BurtGIRadianceCacheClipMapMaxTracesFromMaxUpdateBucketBuffer => GetBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapMaxTracesFromMaxUpdateBucketBufferName);

        public BurtRenderBufferHandle BurtGIRadianceCacheClipMapProbesToUpdateTraceCostBuffer => GetBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbesToUpdateTraceCostBufferName);

        public BurtRenderBufferHandle BurtGIRadianceCacheClipMapRadianceProbePDFBuffer => GetBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapRadianceProbePDFBufferName);

        public BurtRenderBufferHandle BurtGIRadianceCacheClipMapClearProbePDFsIndirectArgsBuffer => GetBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapClearProbePDFsIndirectArgsBufferName);

        public BurtRenderBufferHandle BurtGIRadianceCacheClipMapGenerateProbeTraceTilesIndirectArgsBuffer => GetBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapGenerateProbeTraceTilesIndirectArgsBufferName);

        public BurtRenderBufferHandle BurtGIRadianceCacheClipMapProbeTraceTileAllocatorBuffer => GetBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeTraceTileAllocatorBufferName);

        public BurtRenderBufferHandle BurtGIRadianceCacheClipMapFilterProbesIndirectArgsBuffer => GetBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapFilterProbesIndirectArgsBufferName);

        public BurtRenderBufferHandle BurtGIRadianceCacheClipMapPrepareProbeOcclusionIndirectArgsBuffer => GetBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapPrepareProbeOcclusionIndirectArgsBufferName);

        public BurtRenderBufferHandle BurtGIRadianceCacheClipMapFixupProbeBordersIndirectArgsBuffer => GetBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapFixupProbeBordersIndirectArgsBufferName);

        public BurtRenderBufferHandle BurtGIRadianceCacheClipMapTraceProbesIndirectArgsBuffer => GetBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapTraceProbesIndirectArgsBufferName);

        public BurtRenderBufferHandle BurtGIRadianceCacheClipMapSortProbeTraceTilesIndirectArgsBuffer => GetBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapSortProbeTraceTilesIndirectArgsBufferName);

        public BurtRenderBufferHandle BurtGIRadianceCacheClipMapRadianceCacheHardwareRayTracingIndirectArgsBuffer => GetBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapRadianceCacheHardwareRayTracingIndirectArgsBufferName);

        public BurtRenderBufferHandle BurtGIRadianceCacheClipMapHardwareRayTracingRayAllocatorBuffer => GetBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapHardwareRayTracingRayAllocatorBufferName);

        public BurtRenderBufferHandle BurtGIRadianceCacheClipMapProbeTraceTileDataBuffer => GetBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeTraceTileDataBufferName);

        public BurtRenderBufferHandle BurtGIRadianceCacheClipMapSortedProbeTraceTileDataBuffer => GetBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapSortedProbeTraceTileDataBufferName);

        public BurtRequestRenderOptions RenderOptions { get; private set; } // 保存当前 request 的栈级执行选项，Pass 可以通过它判断 RT 生命周期策略。

        private BurtRenderGraphContext()
        {
        }

        public BurtRenderGraphContext( // 保留旧构造函数，让没有显式传入执行选项的调用方继续走单 request 生命周期。
            ScriptableRenderContext scriptableContext, // 接收 Unity SRP 传入的渲染上下文。
            BurtRenderRequest request, // 接收当前正在执行的 Burt 渲染请求。
            BurtRenderPipelineAsset asset, // 接收 BurtRP 管线资产配置。
            BurtRenderGraphResourceRegistry resourceRegistry) // 接收当前 RenderGraph 的资源注册表。
            : this(scriptableContext, request, asset, resourceRegistry, BurtRequestRenderOptions.CreateSingleRequest()) // 把旧调用统一转发到新构造函数，并使用旧行为默认选项。
        {
        }

        public BurtRenderGraphContext( // 定义新构造函数，用来创建一次带栈级执行选项的 RenderGraph 执行上下文。
            ScriptableRenderContext scriptableContext, // 接收 Unity SRP 传入的渲染上下文。
            BurtRenderRequest request, // 接收当前正在执行的 Burt 渲染请求。
            BurtRenderPipelineAsset asset, // 接收 BurtRP 管线资产配置。
            BurtRenderGraphResourceRegistry resourceRegistry, // 接收当前 RenderGraph 的资源注册表。
            BurtRequestRenderOptions renderOptions) // 接收当前 request 的栈级 RenderTarget 生命周期选项。
            : this(scriptableContext, request, asset, resourceRegistry, renderOptions, null)
        {
        }

        public BurtRenderGraphContext(
            ScriptableRenderContext scriptableContext,
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset,
            BurtRenderGraphResourceRegistry resourceRegistry,
            BurtRequestRenderOptions renderOptions,
            CommandBuffer commandBuffer)
        {
            Initialize(scriptableContext, request, asset, resourceRegistry, renderOptions, commandBuffer);
        }

        public static BurtRenderGraphContext Acquire(
            ScriptableRenderContext scriptableContext,
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset,
            BurtRenderGraphResourceRegistry resourceRegistry,
            BurtRequestRenderOptions renderOptions,
            CommandBuffer commandBuffer)
        {
            var context = ContextPool.Count > 0 ? ContextPool.Pop() : new BurtRenderGraphContext();
            context.Initialize(scriptableContext, request, asset, resourceRegistry, renderOptions, commandBuffer);
            return context;
        }

        public static void Release(BurtRenderGraphContext context)
        {
            if (context == null)
            {
                return;
            }

            context.DiscardCommandBuffer();
            context.ScriptableContext = default;
            context.Request = null;
            context.Asset = null;
            context.ResourceRegistry = null;
            context.RenderOptions = null;
            context.CommandBuffer = null;
            context.graphCommandBuffer = null;
            context.commandBufferHasCommands = false;
            context.graphCommandBufferHasCommands = false;
            context.commandBufferHasWork = false;
            context.graphCommandBufferHasWork = false;
            context.profilingScopesBoundToCurrentQueue = false;
            context.activeProfilingSamplers.Clear();
            context.submittedProfilingSamplers.Clear();
            if (ContextPool.Count < MaxPooledContextCount)
            {
                ContextPool.Push(context);
            }
        }

        private void Initialize(
            ScriptableRenderContext scriptableContext,
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset,
            BurtRenderGraphResourceRegistry resourceRegistry,
            BurtRequestRenderOptions renderOptions,
            CommandBuffer commandBuffer)
        {
            ScriptableContext = scriptableContext; // 把 Unity SRP 渲染上下文保存到 ScriptableContext 属性里。

            Request = request; // 把当前渲染请求保存到 Request 属性里。

            Asset = asset; // 把管线资产保存到 Asset 属性里。

            ResourceRegistry = resourceRegistry; // 把 RenderGraph 的资源注册表保存到 ResourceRegistry 属性里。

            RenderOptions = renderOptions ?? BurtRequestRenderOptions.CreateSingleRequest(); // 保存执行选项，传入空值时回退到旧单 request 生命周期。

            CommandBuffer = commandBuffer;
            graphCommandBuffer = commandBuffer;
            commandBufferHasCommands = false;
            graphCommandBufferHasCommands = false;
            commandBufferHasWork = false;
            graphCommandBufferHasWork = false;
            profilingScopesBoundToCurrentQueue = false;
            activeProfilingSamplers.Clear();
            submittedProfilingSamplers.Clear();
        }

        internal void BeginAsyncPass(CommandBuffer asyncCommandBuffer)
        {
            if (CommandBuffer != graphCommandBuffer)
            {
                return;
            }

            graphCommandBufferHasCommands = commandBufferHasCommands;
            graphCommandBufferHasWork = commandBufferHasWork;
            CommandBuffer = asyncCommandBuffer;
            commandBufferHasCommands = false;
            commandBufferHasWork = false;
            ReopenActiveProfilingScopes();
        }

        internal void EndAsyncPass()
        {
            if (CommandBuffer == graphCommandBuffer)
            {
                return;
            }

            CommandBuffer = graphCommandBuffer;
            commandBufferHasCommands = graphCommandBufferHasCommands;
            commandBufferHasWork = graphCommandBufferHasWork;
            ReopenActiveProfilingScopes();
        }

        public void FlushCommandBuffer()
        {
            FlushCommandBuffer(true);
        }

        internal void FlushCommandBuffer(bool preserveProfilingScopes)
        {
            if (CommandBuffer == null || !commandBufferHasCommands)
            {
                return;
            }

            if (!preserveProfilingScopes)
            {
                CloseActiveProfilingScopes();
            }

            // A synchronous graphics submission does not end the RenderDoc annotation stack.
            // Keep logical RenderGraph/Camera/Stage scopes open across ExecuteCommandBuffer and
            // DrawRenderers, matching XRender's single request-level hierarchy. Marker-only
            // commands are submitted only when they change the queue's visible scope stack.
            var profilingStateChanged = preserveProfilingScopes
                ? !SubmittedProfilingScopesMatchActive()
                : submittedProfilingSamplers.Count > 0;
            var shouldExecute = commandBufferHasWork || profilingStateChanged;
            if (shouldExecute)
            {
                ScriptableContext.ExecuteCommandBuffer(CommandBuffer);
                if (preserveProfilingScopes)
                {
                    SynchronizeSubmittedProfilingScopesWithActive();
                }
                else
                {
                    submittedProfilingSamplers.Clear();
                }
            }

            CommandBuffer.Clear();
            commandBufferHasCommands = false;
            commandBufferHasWork = false;
        }

        internal void ExecuteCurrentCommandBufferAsync(ComputeQueueType queueType)
        {
            if (CommandBuffer == null || !commandBufferHasCommands)
            {
                return;
            }

            CloseActiveProfilingScopes();
            if (commandBufferHasWork)
            {
                ScriptableContext.ExecuteCommandBufferAsync(CommandBuffer, queueType);
            }

            CommandBuffer.Clear();
            commandBufferHasCommands = false;
            commandBufferHasWork = false;
            submittedProfilingSamplers.Clear();
        }

        public void DiscardCommandBuffer()
        {
            CommandBuffer?.Clear();
            CloseSubmittedProfilingScopesAfterDiscard();
            commandBufferHasCommands = false;
            graphCommandBufferHasCommands = false;
            commandBufferHasWork = false;
            graphCommandBufferHasWork = false;
            profilingScopesBoundToCurrentQueue = false;
            activeProfilingSamplers.Clear();
            submittedProfilingSamplers.Clear();
        }

        public CommandBuffer AcquireCommandBuffer(string name)
        {
            if (CommandBuffer != null)
            {
                ReopenActiveProfilingScopes();
                commandBufferHasCommands = true;
                commandBufferHasWork = true;
                return CommandBuffer;
            }

            return CommandBufferPool.Get(string.IsNullOrEmpty(name) ? "BRP.Pass" : name);
        }

        /// <summary>
        /// Records a renderer list into the graph-owned command buffer so scene draws and
        /// profiling markers share one ordered GPU command stream. The legacy DrawRenderers
        /// fallback is only used when the context is executed without a graph command buffer.
        /// </summary>
        public void DrawRendererList(
            CullingResults cullingResults,
            ref DrawingSettings drawingSettings,
            ref FilteringSettings filteringSettings)
        {
            if (CommandBuffer == null)
            {
                ScriptableContext.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
                return;
            }

            ReopenActiveProfilingScopes();
            var rendererListParameters = new RendererListParams(cullingResults, drawingSettings, filteringSettings);
            var rendererList = ScriptableContext.CreateRendererList(ref rendererListParameters);
            CommandBuffer.DrawRendererList(rendererList);
            commandBufferHasCommands = true;
            commandBufferHasWork = true;
        }

        /// <summary>
        /// Records a shadow renderer list into the graph-owned command buffer. Keeping the
        /// caster state, draw, and reset in one stream guarantees that global matrices and
        /// bias values are consumed before the reset commands execute.
        /// </summary>
        public void DrawShadowRendererList(ref ShadowDrawingSettings shadowDrawingSettings)
        {
            if (CommandBuffer == null)
            {
                ScriptableContext.DrawShadows(ref shadowDrawingSettings);
                return;
            }

            ReopenActiveProfilingScopes();
            var rendererList = ScriptableContext.CreateShadowRendererList(ref shadowDrawingSettings);
            CommandBuffer.DrawRendererList(rendererList);
            commandBufferHasCommands = true;
            commandBufferHasWork = true;
        }

        /// <summary>
        /// Releases a pass command buffer only when it is a legacy/local buffer.
        /// Migrated passes receive the graph-owned shared buffer from
        /// <see cref="AcquireCommandBuffer"/>, which must stay alive until the graph finishes.
        /// </summary>
        public void ReleaseCommandBuffer(CommandBuffer commandBuffer)
        {
            if (commandBuffer == null || commandBuffer == CommandBuffer)
            {
                return;
            }

            commandBuffer.Clear();
            CommandBufferPool.Release(commandBuffer);
        }

        internal void MarkCommandBufferHasCommands()
        {
            if (CommandBuffer != null)
            {
                commandBufferHasCommands = true;
                commandBufferHasWork = true;
            }
        }

        internal void BeginProfilingScope(ProfilingSampler sampler)
        {
            if (sampler == null || CommandBuffer == null)
            {
                return;
            }

            ReopenActiveProfilingScopes();
            sampler.Begin(CommandBuffer);
            activeProfilingSamplers.Add(sampler);
            commandBufferHasCommands = true;
            profilingScopesBoundToCurrentQueue = true;
        }

        internal void EndProfilingScope(ProfilingSampler sampler)
        {
            if (sampler == null || CommandBuffer == null || activeProfilingSamplers.Count == 0)
            {
                return;
            }

            ReopenActiveProfilingScopes();
            var lastIndex = activeProfilingSamplers.Count - 1;
            var openedSampler = activeProfilingSamplers[lastIndex];
            activeProfilingSamplers.RemoveAt(lastIndex);
            openedSampler.End(CommandBuffer);
            commandBufferHasCommands = true;
            profilingScopesBoundToCurrentQueue = activeProfilingSamplers.Count > 0;
        }

        public void ExecuteLegacyCommandBuffer(CommandBuffer commandBuffer)
        {
            if (commandBuffer == null || commandBuffer == CommandBuffer)
            {
                return;
            }

            // Keep the logical RenderGraph scopes open while legacy commands execute so their
            // GPU work remains nested under the current Camera/Stage/Pass in RenderDoc.
            FlushCommandBuffer(true);
            ScriptableContext.ExecuteCommandBuffer(commandBuffer);
        }

        public void ExecuteAndReleaseCommandBuffer(CommandBuffer commandBuffer)
        {
            if (commandBuffer == null || commandBuffer == CommandBuffer)
            {
                return;
            }

            try
            {
                ExecuteLegacyCommandBuffer(commandBuffer);
            }
            finally
            {
                ReleaseCommandBuffer(commandBuffer);
            }
        }

        private void CloseActiveProfilingScopes()
        {
            if (CommandBuffer == null || !profilingScopesBoundToCurrentQueue)
            {
                return;
            }

            for (var scopeIndex = activeProfilingSamplers.Count - 1; scopeIndex >= 0; scopeIndex--)
            {
                activeProfilingSamplers[scopeIndex].End(CommandBuffer);
            }

            profilingScopesBoundToCurrentQueue = false;
        }

        private void ReopenActiveProfilingScopes()
        {
            if (CommandBuffer == null || activeProfilingSamplers.Count == 0 || profilingScopesBoundToCurrentQueue)
            {
                return;
            }

            for (var scopeIndex = 0; scopeIndex < activeProfilingSamplers.Count; scopeIndex++)
            {
                activeProfilingSamplers[scopeIndex].Begin(CommandBuffer);
            }

            commandBufferHasCommands = true;
            profilingScopesBoundToCurrentQueue = true;
        }

        private bool SubmittedProfilingScopesMatchActive()
        {
            if (submittedProfilingSamplers.Count != activeProfilingSamplers.Count)
            {
                return false;
            }

            for (var scopeIndex = 0; scopeIndex < activeProfilingSamplers.Count; scopeIndex++)
            {
                if (!ReferenceEquals(submittedProfilingSamplers[scopeIndex], activeProfilingSamplers[scopeIndex]))
                {
                    return false;
                }
            }

            return true;
        }

        private void SynchronizeSubmittedProfilingScopesWithActive()
        {
            submittedProfilingSamplers.Clear();
            for (var scopeIndex = 0; scopeIndex < activeProfilingSamplers.Count; scopeIndex++)
            {
                submittedProfilingSamplers.Add(activeProfilingSamplers[scopeIndex]);
            }
        }

        private void CloseSubmittedProfilingScopesAfterDiscard()
        {
            if (submittedProfilingSamplers.Count == 0 || graphCommandBuffer == null)
            {
                return;
            }

            try
            {
                graphCommandBuffer.Clear();
                for (var scopeIndex = submittedProfilingSamplers.Count - 1; scopeIndex >= 0; scopeIndex--)
                {
                    submittedProfilingSamplers[scopeIndex].End(graphCommandBuffer);
                }

                ScriptableContext.ExecuteCommandBuffer(graphCommandBuffer);
            }
            catch
            {
                // Discard is already running on an exception path. Never replace the original
                // render failure with a secondary profiling-marker cleanup exception.
            }
            finally
            {
                graphCommandBuffer.Clear();
            }
        }
    }
}
