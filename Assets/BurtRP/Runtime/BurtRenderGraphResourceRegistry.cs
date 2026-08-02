using System.Collections.Generic; // 引入泛型集合命名空间，用来使用 Dictionary 和 HashSet 保存资源表与外部导入标记。
using System;
using UnityEngine; // 引入 UnityEngine 命名空间，用来使用 Shader.PropertyToID 生成临时 RT 的整数 ID。
using UnityEngine.Rendering; // 引入 Unity 渲染命名空间，用来使用 RenderTargetIdentifier。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让资源注册表和其他 BurtRP 代码处在同一个模块里。
{
    public readonly struct BurtRenderTextureDescriptor
    {
        public BurtRenderTextureDescriptor(
            RenderTextureDescriptor descriptor,
            FilterMode filterMode = FilterMode.Bilinear,
            string debugName = null)
        {
            Descriptor = descriptor;
            FilterMode = filterMode;
            DebugName = debugName;
        }

        public RenderTextureDescriptor Descriptor { get; }
        public FilterMode FilterMode { get; }
        public string DebugName { get; }
        public bool IsValid => Descriptor.width > 0 && Descriptor.height > 0 && Descriptor.volumeDepth > 0;
    }

    public sealed class BurtRenderGraphResourceRegistry // 定义 RenderGraph 资源注册表，用来集中保存当前图可访问的渲染资源。
    {
        public const string CameraColorName = "CameraColor"; // 定义 BurtRP 中间相机颜色目标的统一资源名，后续所有场景绘制都先写到这个临时颜色 RT。

        public const string IntermediateCameraColorName = CameraColorName; // 给中间颜色 RT 提供更直观的别名，方便后续代码表达“先渲染到中间目标”的语义。

        public const string FinalCameraTargetName = "FinalCameraTarget"; // 定义最终相机输出目标的统一资源名，用来保存 request.TargetIdentifier 指向的 backbuffer 或 targetTexture。

        public const string CameraColorTextureShaderName = "_BurtCameraColorTexture"; // 定义中间颜色 RT 暴露给 shader 的全局纹理名称，FinalBlit 会通过它采样相机颜色。

        public static readonly int CameraColorTextureId = Shader.PropertyToID(CameraColorTextureShaderName); // 把中间颜色 RT 的 shader 名称转换成整数 ID，保证申请、绑定、释放都使用同一个临时 RT。

        public const string OpaqueCameraColorName = "OpaqueCameraColor";

        public const string OpaqueCameraColorTextureShaderName = "_BurtOpaqueCameraColorTexture";

        public static readonly int OpaqueCameraColorTextureId = Shader.PropertyToID(OpaqueCameraColorTextureShaderName);

        public const string OpaqueCameraColorAvailableShaderName = "_BurtOpaqueCameraColorAvailable";

        public static readonly int OpaqueCameraColorAvailableId = Shader.PropertyToID(OpaqueCameraColorAvailableShaderName);

        public const string RefractionDistortionName = "RefractionDistortion";

        public const string RefractionDistortionTextureShaderName = "_BurtRefractionDistortionTexture";

        public static readonly int RefractionDistortionTextureId = Shader.PropertyToID(RefractionDistortionTextureShaderName);

        public const string RefractionSceneColorMipChainName = "RefractionSceneColorMipChain";

        public const string RefractionSceneColorMipChainShaderName = "_BurtRefractionSceneColorMipChain";

        public static readonly int RefractionSceneColorMipChainId = Shader.PropertyToID(RefractionSceneColorMipChainShaderName);

        public const string RefractionAvailableShaderName = "_BurtRefractionAvailable";

        public static readonly int RefractionAvailableId = Shader.PropertyToID(RefractionAvailableShaderName);

        public const string CameraDepthName = "CameraDepth"; // 定义相机深度目标的统一资源名，后续 DepthPrepass、透明排序和后处理会依赖它。

        public const string CameraDepthTextureShaderName = "_BurtCameraDepthTexture"; // 定义真实相机深度 RT 的 shader 名称，后续 shader 采样深度时会使用它。

        public static readonly int CameraDepthTextureId = Shader.PropertyToID(CameraDepthTextureShaderName); // 把 shader 名称转换成整数 ID，CommandBuffer 使用整数 ID 会更稳定也更高效。

        public const string LightShaftOcclusionName = "LightShaftOcclusion";

        public const string LightShaftOcclusionTextureShaderName = "_BurtLightShaftOcclusionTexture";

        public static readonly int LightShaftOcclusionTextureId = Shader.PropertyToID(LightShaftOcclusionTextureShaderName);

        public const string LightShaftOcclusionTempName = "LightShaftOcclusionTemp";

        public const string LightShaftOcclusionTempTextureShaderName = "_BurtLightShaftOcclusionTempTexture";

        public static readonly int LightShaftOcclusionTempTextureId = Shader.PropertyToID(LightShaftOcclusionTempTextureShaderName);

        public const string LightShaftBloomName = "LightShaftBloom";

        public const string LightShaftBloomTextureShaderName = "_BurtLightShaftBloomTexture";

        public static readonly int LightShaftBloomTextureId = Shader.PropertyToID(LightShaftBloomTextureShaderName);

        public const string LightShaftBloomTempName = "LightShaftBloomTemp";

        public const string LightShaftBloomTempTextureShaderName = "_BurtLightShaftBloomTempTexture";

        public static readonly int LightShaftBloomTempTextureId = Shader.PropertyToID(LightShaftBloomTempTextureShaderName);

        public const string FogSourceColorName = "FogSourceColor";

        public const string FogSourceColorTextureShaderName = "_BurtFogSourceColorTexture";

        public static readonly int FogSourceColorTextureId = Shader.PropertyToID(FogSourceColorTextureShaderName);

        public const string VolumetricFogSourceColorName = "VolumetricFogSourceColor";

        public const string VolumetricFogSourceColorTextureShaderName = "_BurtVolumetricFogSourceColorTexture";

        public static readonly int VolumetricFogSourceColorTextureId = Shader.PropertyToID(VolumetricFogSourceColorTextureShaderName);

        public const string AtmosphereAerialSourceColorName = "AtmosphereAerialSourceColor";

        public const string AtmosphereAerialSourceColorTextureShaderName = "_BurtAtmosphereAerialSourceColorTexture";

        public static readonly int AtmosphereAerialSourceColorTextureId = Shader.PropertyToID(AtmosphereAerialSourceColorTextureShaderName);

        public const string DeferredStencilTextureShaderName = "_BurtDeferredStencilTexture";

        public static readonly int DeferredStencilTextureId = Shader.PropertyToID(DeferredStencilTextureShaderName);

        public const string DeferredStencilTexelSizeShaderName = "_BurtDeferredStencilTexelSize";

        public static readonly int DeferredStencilTexelSizeId = Shader.PropertyToID(DeferredStencilTexelSizeShaderName);

        public const string DeferredStencilTextureAvailableShaderName = "_BurtDeferredStencilTextureAvailable";

        public static readonly int DeferredStencilTextureAvailableId = Shader.PropertyToID(DeferredStencilTextureAvailableShaderName);

        public const string DeferredLightingDepthName = "DeferredLightingDepth";

        public const string DeferredLightingDepthTextureShaderName = "_BurtDeferredLightingDepthTexture";

        public static readonly int DeferredLightingDepthTextureId = Shader.PropertyToID(DeferredLightingDepthTextureShaderName);

        public const string PostProcessColorName = "PostProcessColor"; // 定义后处理中间颜色目标的统一资源名，No-op Copy 和后续效果链会通过它做 ping-pong。

        public const string PostProcessColorTextureShaderName = "_BurtPostProcessColorTexture"; // 定义后处理中间颜色 RT 暴露给 shader 的全局纹理名称。

        public static readonly int PostProcessColorTextureId = Shader.PropertyToID(PostProcessColorTextureShaderName); // 把后处理中间颜色名称转换成整数 ID，申请、绑定和释放都会复用它。

        public const int BloomPyramidCount = 6;

        public const string BloomInputName = "BloomInputHalfResolution";

        public const string BloomInputTextureShaderName = "_BurtBloomInputTexture";

        public static readonly int BloomInputTextureId = Shader.PropertyToID(BloomInputTextureShaderName);

        public const string BloomSetupName = "BloomSetup";

        public const string BloomSetupTextureShaderName = "_BurtBloomSetupTexture";

        public static readonly int BloomSetupTextureId = Shader.PropertyToID(BloomSetupTextureShaderName);

        private static readonly string[] BloomDownsampleNames = CreateBloomResourceNames("BloomDownsample");

        private static readonly string[] BloomGaussianHorizontalNames = CreateBloomResourceNames("BloomGaussianHorizontal");

        private static readonly string[] BloomGaussianVerticalNames = CreateBloomResourceNames("BloomGaussianVertical");

        private static readonly int[] BloomDownsampleTextureIds = CreateBloomTextureIds("_BurtBloomDownsample");

        private static readonly int[] BloomGaussianHorizontalTextureIds = CreateBloomTextureIds("_BurtBloomGaussianHorizontal");

        private static readonly int[] BloomGaussianVerticalTextureIds = CreateBloomTextureIds("_BurtBloomGaussianVertical");

        public const string TemporalAAOutputName = "TemporalAAOutput";

        public const string TemporalAAOutputTextureShaderName = "_BurtTemporalAAOutputTexture";

        public static readonly int TemporalAAOutputTextureId = Shader.PropertyToID(TemporalAAOutputTextureShaderName);

        public const string GBuffer0Name = "GBuffer0"; // 定义 Deferred 第一张 GBuffer 的统一资源名，用于保存 DepthNormals prepass 写入的 normal 和 perceptual roughness。

        public const string GBuffer0ShaderName = "_BurtGBuffer0"; // 定义 GBuffer0 暴露给 shader 的全局纹理名称，Deferred Lighting 会采样它。

        public static readonly int GBuffer0Id = Shader.PropertyToID(GBuffer0ShaderName); // 把 GBuffer0 shader 名称转换成整数 ID，后续申请、绑定和释放都会复用它。

        public const string GBuffer1Name = "GBuffer1"; // 定义 Deferred 第二张 GBuffer 的统一资源名，用于保存 baseColor 和 occlusion。

        public const string GBuffer1ShaderName = "_BurtGBuffer1"; // 定义 GBuffer1 暴露给 shader 的全局纹理名称，Deferred Lighting 会采样它。

        public static readonly int GBuffer1Id = Shader.PropertyToID(GBuffer1ShaderName); // 把 GBuffer1 shader 名称转换成整数 ID，后续申请、绑定和释放都会复用它。

        public const string GBuffer2Name = "GBuffer2"; // 定义 Deferred 第三张 GBuffer 的统一资源名，用于保存 shading model/material channel、metallic、smoothness 和 reflectance。

        public const string GBuffer2ShaderName = "_BurtGBuffer2"; // 定义 GBuffer2 暴露给 shader 的全局纹理名称，Deferred Lighting 会采样它。

        public static readonly int GBuffer2Id = Shader.PropertyToID(GBuffer2ShaderName); // 把 GBuffer2 shader 名称转换成整数 ID，后续申请、绑定和释放都会复用它。

        public const string GBuffer3Name = "GBuffer3"; // 定义 Deferred 第四张 GBuffer 的统一资源名，用于保存 Clear Coat 独立法线、mask 和 roughness。

        public const string GBuffer3ShaderName = "_BurtGBuffer3"; // 定义 GBuffer3 暴露给 shader 的全局纹理名称，Deferred Lighting 会采样它。

        public static readonly int GBuffer3Id = Shader.PropertyToID(GBuffer3ShaderName); // 把 GBuffer3 shader 名称转换成整数 ID，后续申请、绑定和释放都会复用它。

        public const string GBuffer4Name = "GBuffer4";

        public const string GBuffer4ShaderName = "_BurtGBuffer4";

        public static readonly int GBuffer4Id = Shader.PropertyToID(GBuffer4ShaderName);

        public const string GBuffer5Name = "GBuffer5";

        public const string GBuffer5ShaderName = "_BurtGBuffer5";

        public static readonly int GBuffer5Id = Shader.PropertyToID(GBuffer5ShaderName);

        public const string GBufferObjectIndexName = "GBufferObjectIndex";

        public const string GBufferObjectIndexShaderName = "_BurtGBufferObjectIndex";

        public static readonly int GBufferObjectIndexId = Shader.PropertyToID(GBufferObjectIndexShaderName);

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

        public const string ScreenSpaceShadowName = "ScreenSpaceShadow";

        public const string ScreenSpaceShadowTextureShaderName = "_BurtScreenSpaceShadowTexture";

        public static readonly int ScreenSpaceShadowTextureId = Shader.PropertyToID(ScreenSpaceShadowTextureShaderName);

        public const string ScreenSpaceGlobalIlluminationRawName = "ScreenSpaceGlobalIlluminationRaw";

        public const string ScreenSpaceGlobalIlluminationRawTextureShaderName = "_BurtScreenSpaceGlobalIlluminationRawTexture";

        public static readonly int ScreenSpaceGlobalIlluminationRawTextureId = Shader.PropertyToID(ScreenSpaceGlobalIlluminationRawTextureShaderName);

        public const string ScreenSpaceGlobalIlluminationName = "ScreenSpaceGlobalIllumination";

        public const string ScreenSpaceGlobalIlluminationTextureShaderName = "_BurtScreenSpaceGlobalIlluminationTexture";

        public static readonly int ScreenSpaceGlobalIlluminationTextureId = Shader.PropertyToID(ScreenSpaceGlobalIlluminationTextureShaderName);

        public const string ScreenSpaceGlobalIlluminationUpsampledName = "ScreenSpaceGlobalIlluminationUpsampled";

        public const string ScreenSpaceGlobalIlluminationUpsampledTextureShaderName = "_BurtScreenSpaceGlobalIlluminationUpsampledTexture";

        public static readonly int ScreenSpaceGlobalIlluminationUpsampledTextureId = Shader.PropertyToID(ScreenSpaceGlobalIlluminationUpsampledTextureShaderName);

        public const string BurtGIBackfaceDiffuseIndirectName = "BurtGIBackfaceDiffuseIndirect";

        public const string BurtGIBackfaceDiffuseIndirectTextureShaderName = "_BurtGIBackfaceDiffuseIndirectTexture";

        public static readonly int BurtGIBackfaceDiffuseIndirectTextureId = Shader.PropertyToID(BurtGIBackfaceDiffuseIndirectTextureShaderName);

        public const string BurtGIBackfaceDiffuseIndirectUpsampledName = "BurtGIBackfaceDiffuseIndirectUpsampled";

        public const string BurtGIBackfaceDiffuseIndirectUpsampledTextureShaderName = "_BurtGIBackfaceDiffuseIndirectUpsampledTexture";

        public static readonly int BurtGIBackfaceDiffuseIndirectUpsampledTextureId = Shader.PropertyToID(BurtGIBackfaceDiffuseIndirectUpsampledTextureShaderName);

        public const string BurtGIRoughSpecularIndirectName = "BurtGIRoughSpecularIndirect";

        public const string BurtGIRoughSpecularIndirectTextureShaderName = "_BurtGIRoughSpecularIndirectTexture";

        public static readonly int BurtGIRoughSpecularIndirectTextureId = Shader.PropertyToID(BurtGIRoughSpecularIndirectTextureShaderName);

        public const string BurtGIRoughSpecularIndirectUpsampledName = "BurtGIRoughSpecularIndirectUpsampled";

        public const string BurtGIRoughSpecularIndirectUpsampledTextureShaderName = "_BurtGIRoughSpecularIndirectUpsampledTexture";

        public static readonly int BurtGIRoughSpecularIndirectUpsampledTextureId = Shader.PropertyToID(BurtGIRoughSpecularIndirectUpsampledTextureShaderName);

        public const string BurtGIScreenProbeIntegrateTileClassificationName = "BurtGIScreenProbeIntegrateTileClassification";

        public const string BurtGIScreenProbeIntegrateTileClassificationTextureShaderName = "_BurtGIScreenProbeIntegrateTileClassificationTexture";

        public static readonly int BurtGIScreenProbeIntegrateTileClassificationTextureId = Shader.PropertyToID(BurtGIScreenProbeIntegrateTileClassificationTextureShaderName);

        public const string BurtGITranslucencyVolume0Name = "BurtGITranslucencyVolume0";

        public const string BurtGITranslucencyVolume0TextureShaderName = "_BurtGITranslucencyVolume0";

        public static readonly int BurtGITranslucencyVolume0TextureId = Shader.PropertyToID(BurtGITranslucencyVolume0TextureShaderName);

        public const string BurtGITranslucencyVolume1Name = "BurtGITranslucencyVolume1";

        public const string BurtGITranslucencyVolume1TextureShaderName = "_BurtGITranslucencyVolume1";

        public static readonly int BurtGITranslucencyVolume1TextureId = Shader.PropertyToID(BurtGITranslucencyVolume1TextureShaderName);

        public const string BurtGITranslucencyVolumeFilter0Name = "BurtGITranslucencyVolumeFilter0";

        public const string BurtGITranslucencyVolumeFilter0TextureShaderName = "_BurtGITranslucencyVolumeFilter0";

        public static readonly int BurtGITranslucencyVolumeFilter0TextureId = Shader.PropertyToID(BurtGITranslucencyVolumeFilter0TextureShaderName);

        public const string BurtGITranslucencyVolumeFilter1Name = "BurtGITranslucencyVolumeFilter1";

        public const string BurtGITranslucencyVolumeFilter1TextureShaderName = "_BurtGITranslucencyVolumeFilter1";

        public static readonly int BurtGITranslucencyVolumeFilter1TextureId = Shader.PropertyToID(BurtGITranslucencyVolumeFilter1TextureShaderName);

        public const string BurtGITranslucencyVolumeTraceRadianceName = "BurtGITranslucencyVolumeTraceRadiance";

        public const string BurtGITranslucencyVolumeTraceRadianceTextureShaderName = "_BurtGITranslucencyVolumeTraceRadiance";

        public static readonly int BurtGITranslucencyVolumeTraceRadianceTextureId = Shader.PropertyToID(BurtGITranslucencyVolumeTraceRadianceTextureShaderName);

        public const string BurtGITranslucencyVolumeTraceFilteredRadianceName = "BurtGITranslucencyVolumeTraceFilteredRadiance";

        public const string BurtGITranslucencyVolumeTraceFilteredRadianceTextureShaderName = "_BurtGITranslucencyVolumeTraceFilteredRadiance";

        public static readonly int BurtGITranslucencyVolumeTraceFilteredRadianceTextureId = Shader.PropertyToID(BurtGITranslucencyVolumeTraceFilteredRadianceTextureShaderName);

        public const string BurtGITranslucencyVolumeTraceHitDistanceName = "BurtGITranslucencyVolumeTraceHitDistance";

        public const string BurtGITranslucencyVolumeTraceHitDistanceTextureShaderName = "_BurtGITranslucencyVolumeTraceHitDistance";

        public static readonly int BurtGITranslucencyVolumeTraceHitDistanceTextureId = Shader.PropertyToID(BurtGITranslucencyVolumeTraceHitDistanceTextureShaderName);

        public const string BurtGISceneVoxelRadianceName = "BurtGISceneVoxelRadiance";

        public const string BurtGISceneVoxelRadianceTextureShaderName = "_BurtGISceneVoxelRadianceTexture";

        public const string BurtGISceneVoxelRadianceReadTextureShaderName = "_BurtGISceneVoxelRadianceReadTexture";

        public static readonly int BurtGISceneVoxelRadianceTextureId = Shader.PropertyToID(BurtGISceneVoxelRadianceTextureShaderName);

        public static readonly int BurtGISceneVoxelRadianceReadTextureId = Shader.PropertyToID(BurtGISceneVoxelRadianceReadTextureShaderName);

        public const string BurtGISceneVoxelGeometryName = "BurtGISceneVoxelGeometry";

        public const string BurtGISceneVoxelGeometryTextureShaderName = "_BurtGISceneVoxelGeometryTexture";

        public const string BurtGISceneVoxelGeometryReadTextureShaderName = "_BurtGISceneVoxelGeometryReadTexture";

        public static readonly int BurtGISceneVoxelGeometryTextureId = Shader.PropertyToID(BurtGISceneVoxelGeometryTextureShaderName);

        public static readonly int BurtGISceneVoxelGeometryReadTextureId = Shader.PropertyToID(BurtGISceneVoxelGeometryReadTextureShaderName);

        public const string BurtGISceneVoxelOccupancyMipName = "BurtGISceneVoxelOccupancyMip";

        public const string BurtGISceneVoxelOccupancyMipTextureShaderName = "_BurtGISceneVoxelOccupancyMipTexture";

        public const string BurtGISceneVoxelOccupancyMipReadTextureShaderName = "_BurtGISceneVoxelOccupancyMipReadTexture";

        public static readonly int BurtGISceneVoxelOccupancyMipTextureId = Shader.PropertyToID(BurtGISceneVoxelOccupancyMipTextureShaderName);

        public static readonly int BurtGISceneVoxelOccupancyMipReadTextureId = Shader.PropertyToID(BurtGISceneVoxelOccupancyMipReadTextureShaderName);

        public const string BurtGISceneVoxelLightingName = "BurtGISceneVoxelLighting";

        public const string BurtGISceneVoxelLightingTextureShaderName = "_BurtGISceneVoxelLightingTexture";

        public const string BurtGISceneVoxelLightingReadTextureShaderName = "_BurtGISceneVoxelLightingReadTexture";

        public static readonly int BurtGISceneVoxelLightingTextureId = Shader.PropertyToID(BurtGISceneVoxelLightingTextureShaderName);

        public static readonly int BurtGISceneVoxelLightingReadTextureId = Shader.PropertyToID(BurtGISceneVoxelLightingReadTextureShaderName);

        public const string BurtGITemporalDiagnosticsName = "BurtGITemporalDiagnostics";

        public const string BurtGITemporalDiagnosticsTextureShaderName = "_BurtGITemporalDiagnosticsTexture";

        public static readonly int BurtGITemporalDiagnosticsTextureId = Shader.PropertyToID(BurtGITemporalDiagnosticsTextureShaderName);

        public const string BurtGIRadianceCacheStatsName = "BurtGIRadianceCacheStats";

        public const string BurtGIRadianceCacheStatsTextureShaderName = "_BurtGIRadianceCacheStatsTexture";

        public static readonly int BurtGIRadianceCacheStatsTextureId = Shader.PropertyToID(BurtGIRadianceCacheStatsTextureShaderName);

        public const string BurtGIScreenProbeRadianceName = "BurtGIScreenProbeRadiance";

        public const string BurtGIScreenProbeRadianceTextureShaderName = "_BurtGIScreenProbeRadianceTexture";

        public static readonly int BurtGIScreenProbeRadianceTextureId = Shader.PropertyToID(BurtGIScreenProbeRadianceTextureShaderName);

        public const string BurtGIScreenProbeIrradianceName = "BurtGIScreenProbeIrradiance";

        public const string BurtGIScreenProbeIrradianceTextureShaderName = "_BurtGIScreenProbeIrradianceTexture";

        public static readonly int BurtGIScreenProbeIrradianceTextureId = Shader.PropertyToID(BurtGIScreenProbeIrradianceTextureShaderName);

        public const string BurtGIScreenProbeConfidenceName = "BurtGIScreenProbeConfidence";

        public const string BurtGIScreenProbeConfidenceTextureShaderName = "_BurtGIScreenProbeConfidenceTexture";

        public static readonly int BurtGIScreenProbeConfidenceTextureId = Shader.PropertyToID(BurtGIScreenProbeConfidenceTextureShaderName);

        public const string BurtGIScreenProbeHitDistanceName = "BurtGIScreenProbeHitDistance";

        public const string BurtGIScreenProbeHitDistanceTextureShaderName = "_BurtGIScreenProbeHitDistanceTexture";

        public static readonly int BurtGIScreenProbeHitDistanceTextureId = Shader.PropertyToID(BurtGIScreenProbeHitDistanceTextureShaderName);

        public const string BurtGIScreenProbeBentNormalName = "BurtGIScreenProbeBentNormal";

        public const string BurtGIScreenProbeBentNormalTextureShaderName = "_BurtGIScreenProbeBentNormalTexture";

        public static readonly int BurtGIScreenProbeBentNormalTextureId = Shader.PropertyToID(BurtGIScreenProbeBentNormalTextureShaderName);

        public const string BurtGIScreenProbeScreenDepthName = "BurtGIScreenProbeScreenDepth";

        public const string BurtGIScreenProbeScreenDepthTextureShaderName = "_BurtGIScreenProbeScreenDepthTexture";

        public static readonly int BurtGIScreenProbeScreenDepthTextureId = Shader.PropertyToID(BurtGIScreenProbeScreenDepthTextureShaderName);

        public const string BurtGIScreenProbeWorldNormalName = "BurtGIScreenProbeWorldNormal";

        public const string BurtGIScreenProbeWorldNormalTextureShaderName = "_BurtGIScreenProbeWorldNormalTexture";

        public static readonly int BurtGIScreenProbeWorldNormalTextureId = Shader.PropertyToID(BurtGIScreenProbeWorldNormalTextureShaderName);

        public const string BurtGIScreenProbeWorldPositionName = "BurtGIScreenProbeWorldPosition";

        public const string BurtGIScreenProbeWorldPositionTextureShaderName = "_BurtGIScreenProbeWorldPositionTexture";

        public static readonly int BurtGIScreenProbeWorldPositionTextureId = Shader.PropertyToID(BurtGIScreenProbeWorldPositionTextureShaderName);

        public const string BurtGIScreenProbeAdaptiveProbeHeaderName = "BurtGIScreenProbeAdaptiveProbeHeader";

        public const string BurtGIScreenProbeAdaptiveProbeHeaderTextureShaderName = "_BurtGIScreenProbeAdaptiveProbeHeaderTexture";

        public static readonly int BurtGIScreenProbeAdaptiveProbeHeaderTextureId = Shader.PropertyToID(BurtGIScreenProbeAdaptiveProbeHeaderTextureShaderName);

        public const string BurtGIScreenProbeAdaptiveProbeIndicesName = "BurtGIScreenProbeAdaptiveProbeIndices";

        public const string BurtGIScreenProbeAdaptiveProbeIndicesTextureShaderName = "_BurtGIScreenProbeAdaptiveProbeIndicesTexture";

        public static readonly int BurtGIScreenProbeAdaptiveProbeIndicesTextureId = Shader.PropertyToID(BurtGIScreenProbeAdaptiveProbeIndicesTextureShaderName);

        public const string BurtGIScreenProbeTraceRadianceName = "BurtGIScreenProbeTraceRadiance";

        public const string BurtGIScreenProbeTraceRadianceTextureShaderName = "_BurtGIScreenProbeTraceRadianceTexture";

        public static readonly int BurtGIScreenProbeTraceRadianceTextureId = Shader.PropertyToID(BurtGIScreenProbeTraceRadianceTextureShaderName);

        public const string BurtGIScreenProbeTraceHitName = "BurtGIScreenProbeTraceHit";

        public const string BurtGIScreenProbeTraceHitTextureShaderName = "_BurtGIScreenProbeTraceHitTexture";

        public static readonly int BurtGIScreenProbeTraceHitTextureId = Shader.PropertyToID(BurtGIScreenProbeTraceHitTextureShaderName);

        public const string BurtGIScreenProbeTemporalRadianceName = "BurtGIScreenProbeTemporalRadiance";

        public const string BurtGIScreenProbeTemporalRadianceTextureShaderName = "_BurtGIScreenProbeTemporalRadianceTexture";

        public static readonly int BurtGIScreenProbeTemporalRadianceTextureId = Shader.PropertyToID(BurtGIScreenProbeTemporalRadianceTextureShaderName);

        public const string BurtGIScreenProbeTemporalIrradianceName = "BurtGIScreenProbeTemporalIrradiance";

        public const string BurtGIScreenProbeTemporalIrradianceTextureShaderName = "_BurtGIScreenProbeTemporalIrradianceTexture";

        public static readonly int BurtGIScreenProbeTemporalIrradianceTextureId = Shader.PropertyToID(BurtGIScreenProbeTemporalIrradianceTextureShaderName);

        public const string BurtGIScreenProbeTemporalConfidenceName = "BurtGIScreenProbeTemporalConfidence";

        public const string BurtGIScreenProbeTemporalConfidenceTextureShaderName = "_BurtGIScreenProbeTemporalConfidenceTexture";

        public static readonly int BurtGIScreenProbeTemporalConfidenceTextureId = Shader.PropertyToID(BurtGIScreenProbeTemporalConfidenceTextureShaderName);

        public const string BurtGIScreenProbeFilteredRadianceName = "BurtGIScreenProbeFilteredRadiance";

        public const string BurtGIScreenProbeFilteredRadianceTextureShaderName = "_BurtGIScreenProbeFilteredRadianceTexture";

        public static readonly int BurtGIScreenProbeFilteredRadianceTextureId = Shader.PropertyToID(BurtGIScreenProbeFilteredRadianceTextureShaderName);

        public const string BurtGIScreenProbeFilteredIrradianceName = "BurtGIScreenProbeFilteredIrradiance";

        public const string BurtGIScreenProbeFilteredIrradianceTextureShaderName = "_BurtGIScreenProbeFilteredIrradianceTexture";

        public static readonly int BurtGIScreenProbeFilteredIrradianceTextureId = Shader.PropertyToID(BurtGIScreenProbeFilteredIrradianceTextureShaderName);

        public const string BurtGIScreenProbeFilteredConfidenceName = "BurtGIScreenProbeFilteredConfidence";

        public const string BurtGIScreenProbeFilteredConfidenceTextureShaderName = "_BurtGIScreenProbeFilteredConfidenceTexture";

        public static readonly int BurtGIScreenProbeFilteredConfidenceTextureId = Shader.PropertyToID(BurtGIScreenProbeFilteredConfidenceTextureShaderName);

        public const string BurtGIScreenProbeFixupRadianceName = "BurtGIScreenProbeFixupRadiance";

        public const string BurtGIScreenProbeFixupRadianceTextureShaderName = "_BurtGIScreenProbeFixupRadianceTexture";

        public static readonly int BurtGIScreenProbeFixupRadianceTextureId = Shader.PropertyToID(BurtGIScreenProbeFixupRadianceTextureShaderName);

        public const string BurtGIScreenProbeFixupIrradianceName = "BurtGIScreenProbeFixupIrradiance";

        public const string BurtGIScreenProbeFixupIrradianceTextureShaderName = "_BurtGIScreenProbeFixupIrradianceTexture";

        public static readonly int BurtGIScreenProbeFixupIrradianceTextureId = Shader.PropertyToID(BurtGIScreenProbeFixupIrradianceTextureShaderName);

        public const string BurtGIScreenProbeFixupConfidenceName = "BurtGIScreenProbeFixupConfidence";

        public const string BurtGIScreenProbeFixupConfidenceTextureShaderName = "_BurtGIScreenProbeFixupConfidenceTexture";

        public static readonly int BurtGIScreenProbeFixupConfidenceTextureId = Shader.PropertyToID(BurtGIScreenProbeFixupConfidenceTextureShaderName);

        public const string BurtGIScreenProbeMipRadianceName = "BurtGIScreenProbeMipRadiance";

        public const string BurtGIScreenProbeMipRadianceTextureShaderName = "_BurtGIScreenProbeMipRadianceTexture";

        public static readonly int BurtGIScreenProbeMipRadianceTextureId = Shader.PropertyToID(BurtGIScreenProbeMipRadianceTextureShaderName);

        public const string BurtGIScreenProbeMipIrradianceName = "BurtGIScreenProbeMipIrradiance";

        public const string BurtGIScreenProbeMipIrradianceTextureShaderName = "_BurtGIScreenProbeMipIrradianceTexture";

        public static readonly int BurtGIScreenProbeMipIrradianceTextureId = Shader.PropertyToID(BurtGIScreenProbeMipIrradianceTextureShaderName);

        public const string BurtGIScreenProbeMipConfidenceName = "BurtGIScreenProbeMipConfidence";

        public const string BurtGIScreenProbeMipConfidenceTextureShaderName = "_BurtGIScreenProbeMipConfidenceTexture";

        public static readonly int BurtGIScreenProbeMipConfidenceTextureId = Shader.PropertyToID(BurtGIScreenProbeMipConfidenceTextureShaderName);

        public const string BurtGIScreenProbeMip2RadianceName = "BurtGIScreenProbeMip2Radiance";

        public const string BurtGIScreenProbeMip2RadianceTextureShaderName = "_BurtGIScreenProbeMip2RadianceTexture";

        public static readonly int BurtGIScreenProbeMip2RadianceTextureId = Shader.PropertyToID(BurtGIScreenProbeMip2RadianceTextureShaderName);

        public const string BurtGIScreenProbeMip2IrradianceName = "BurtGIScreenProbeMip2Irradiance";

        public const string BurtGIScreenProbeMip2IrradianceTextureShaderName = "_BurtGIScreenProbeMip2IrradianceTexture";

        public static readonly int BurtGIScreenProbeMip2IrradianceTextureId = Shader.PropertyToID(BurtGIScreenProbeMip2IrradianceTextureShaderName);

        public const string BurtGIScreenProbeMip2ConfidenceName = "BurtGIScreenProbeMip2Confidence";

        public const string BurtGIScreenProbeMip2ConfidenceTextureShaderName = "_BurtGIScreenProbeMip2ConfidenceTexture";

        public static readonly int BurtGIScreenProbeMip2ConfidenceTextureId = Shader.PropertyToID(BurtGIScreenProbeMip2ConfidenceTextureShaderName);

        public const string BurtGIScreenProbeMip3RadianceName = "BurtGIScreenProbeMip3Radiance";

        public const string BurtGIScreenProbeMip3RadianceTextureShaderName = "_BurtGIScreenProbeMip3RadianceTexture";

        public static readonly int BurtGIScreenProbeMip3RadianceTextureId = Shader.PropertyToID(BurtGIScreenProbeMip3RadianceTextureShaderName);

        public const string BurtGIScreenProbeMip3IrradianceName = "BurtGIScreenProbeMip3Irradiance";

        public const string BurtGIScreenProbeMip3IrradianceTextureShaderName = "_BurtGIScreenProbeMip3IrradianceTexture";

        public static readonly int BurtGIScreenProbeMip3IrradianceTextureId = Shader.PropertyToID(BurtGIScreenProbeMip3IrradianceTextureShaderName);

        public const string BurtGIScreenProbeMip3ConfidenceName = "BurtGIScreenProbeMip3Confidence";

        public const string BurtGIScreenProbeMip3ConfidenceTextureShaderName = "_BurtGIScreenProbeMip3ConfidenceTexture";

        public static readonly int BurtGIScreenProbeMip3ConfidenceTextureId = Shader.PropertyToID(BurtGIScreenProbeMip3ConfidenceTextureShaderName);

        public const string BurtGIScreenProbeRadianceSHAmbientName = "BurtGIScreenProbeRadianceSHAmbient";

        public const string BurtGIScreenProbeRadianceSHAmbientTextureShaderName = "_BurtGIScreenProbeRadianceSHAmbientTexture";

        public static readonly int BurtGIScreenProbeRadianceSHAmbientTextureId = Shader.PropertyToID(BurtGIScreenProbeRadianceSHAmbientTextureShaderName);

        public const string BurtGIScreenProbeRadianceSHDirectionalName = "BurtGIScreenProbeRadianceSHDirectional";

        public const string BurtGIScreenProbeRadianceSHDirectionalTextureShaderName = "_BurtGIScreenProbeRadianceSHDirectionalTexture";

        public static readonly int BurtGIScreenProbeRadianceSHDirectionalTextureId = Shader.PropertyToID(BurtGIScreenProbeRadianceSHDirectionalTextureShaderName);

        public const string BurtGIScreenProbeIrradianceOctName = "BurtGIScreenProbeIrradianceOct";

        public const string BurtGIScreenProbeIrradianceOctTextureShaderName = "_BurtGIScreenProbeIrradianceOctTexture";

        public static readonly int BurtGIScreenProbeIrradianceOctTextureId = Shader.PropertyToID(BurtGIScreenProbeIrradianceOctTextureShaderName);

        public const string BurtGIScreenProbeRadianceOctName = "BurtGIScreenProbeRadianceOct";

        public const string BurtGIScreenProbeRadianceOctTextureShaderName = "_BurtGIScreenProbeRadianceOctTexture";

        public static readonly int BurtGIScreenProbeRadianceOctTextureId = Shader.PropertyToID(BurtGIScreenProbeRadianceOctTextureShaderName);

        public const string BurtGIScreenProbeImportancePDFName = "BurtGIScreenProbeImportancePDF";

        public const string BurtGIScreenProbeImportancePDFTextureShaderName = "_BurtGIScreenProbeImportancePDFTexture";

        public static readonly int BurtGIScreenProbeImportancePDFTextureId = Shader.PropertyToID(BurtGIScreenProbeImportancePDFTextureShaderName);

        public const string BurtGIScreenProbeImportancePDFSHBufferName = "BurtGIScreenProbeImportancePDFSHBuffer";

        public const string BurtGIScreenProbeImportancePDFSHBufferShaderName = "_BurtGIScreenProbeImportancePDFSHBuffer";

        public static readonly int BurtGIScreenProbeImportancePDFSHBufferId = Shader.PropertyToID(BurtGIScreenProbeImportancePDFSHBufferShaderName);

        public const string BurtGIScreenProbeImportanceLightPDFName = "BurtGIScreenProbeImportanceLightPDF";

        public const string BurtGIScreenProbeImportanceLightPDFTextureShaderName = "_BurtGIScreenProbeImportanceLightPDFTexture";

        public static readonly int BurtGIScreenProbeImportanceLightPDFTextureId = Shader.PropertyToID(BurtGIScreenProbeImportanceLightPDFTextureShaderName);

        public const string BurtGIScreenProbeImportanceRayInfoName = "BurtGIScreenProbeImportanceRayInfo";

        public const string BurtGIScreenProbeImportanceRayInfoTextureShaderName = "_BurtGIScreenProbeImportanceRayInfoTexture";

        public static readonly int BurtGIScreenProbeImportanceRayInfoTextureId = Shader.PropertyToID(BurtGIScreenProbeImportanceRayInfoTextureShaderName);

        public const string BurtGIRadianceCacheClipMapIndirectionName = "BurtGIRadianceCacheClipMapIndirection";

        public const string BurtGIRadianceCacheClipMapIndirectionTextureShaderName = "_BurtGIRadianceCacheClipMapIndirectionTexture";

        public static readonly int BurtGIRadianceCacheClipMapIndirectionTextureId = Shader.PropertyToID(BurtGIRadianceCacheClipMapIndirectionTextureShaderName);

        public const string BurtGIRadianceCacheClipMapDepthProbeAtlasName = "BurtGIRadianceCacheClipMapDepthProbeAtlas";

        public const string BurtGIRadianceCacheClipMapDepthProbeAtlasTextureShaderName = "_BurtGIRadianceCacheClipMapDepthProbeAtlasTexture";

        public static readonly int BurtGIRadianceCacheClipMapDepthProbeAtlasTextureId = Shader.PropertyToID(BurtGIRadianceCacheClipMapDepthProbeAtlasTextureShaderName);

        public const string BurtGIRadianceCacheClipMapRadianceProbeAtlasName = "BurtGIRadianceCacheClipMapRadianceProbeAtlas";

        public const string BurtGIRadianceCacheClipMapRadianceProbeAtlasTextureShaderName = "_BurtGIRadianceCacheClipMapRadianceProbeAtlasTexture";

        public static readonly int BurtGIRadianceCacheClipMapRadianceProbeAtlasTextureId = Shader.PropertyToID(BurtGIRadianceCacheClipMapRadianceProbeAtlasTextureShaderName);

        public const string BurtGIRadianceCacheClipMapFinalRadianceAtlasName = "BurtGIRadianceCacheClipMapFinalRadianceAtlas";

        public const string BurtGIRadianceCacheClipMapFinalRadianceAtlasTextureShaderName = "_BurtGIRadianceCacheClipMapFinalRadianceAtlasTexture";

        public static readonly int BurtGIRadianceCacheClipMapFinalRadianceAtlasTextureId = Shader.PropertyToID(BurtGIRadianceCacheClipMapFinalRadianceAtlasTextureShaderName);

        public const string BurtGIRadianceCacheClipMapFinalIrradianceAtlasName = "BurtGIRadianceCacheClipMapFinalIrradianceAtlas";

        public const string BurtGIRadianceCacheClipMapFinalIrradianceAtlasTextureShaderName = "_BurtGIRadianceCacheClipMapFinalIrradianceAtlasTexture";

        public static readonly int BurtGIRadianceCacheClipMapFinalIrradianceAtlasTextureId = Shader.PropertyToID(BurtGIRadianceCacheClipMapFinalIrradianceAtlasTextureShaderName);

        public const string BurtGIRadianceCacheClipMapProbeOcclusionAtlasName = "BurtGIRadianceCacheClipMapProbeOcclusionAtlas";

        public const string BurtGIRadianceCacheClipMapProbeOcclusionAtlasTextureShaderName = "_BurtGIRadianceCacheClipMapProbeOcclusionAtlasTexture";

        public static readonly int BurtGIRadianceCacheClipMapProbeOcclusionAtlasTextureId = Shader.PropertyToID(BurtGIRadianceCacheClipMapProbeOcclusionAtlasTextureShaderName);

        public const string BurtGIRadianceCacheClipMapProbeSkyAOAtlasName = "BurtGIRadianceCacheClipMapProbeSkyAOAtlas";

        public const string BurtGIRadianceCacheClipMapProbeSkyAOAtlasTextureShaderName = "_BurtGIRadianceCacheClipMapProbeSkyAOAtlasTexture";

        public static readonly int BurtGIRadianceCacheClipMapProbeSkyAOAtlasTextureId = Shader.PropertyToID(BurtGIRadianceCacheClipMapProbeSkyAOAtlasTextureShaderName);

        public const string ScreenSpaceSubsurfaceSourceName = "ScreenSpaceSubsurfaceSource";

        public const string ScreenSpaceSubsurfaceSourceTextureShaderName = "_BurtScreenSpaceSubsurfaceSourceTexture";

        public static readonly int ScreenSpaceSubsurfaceSourceTextureId = Shader.PropertyToID(ScreenSpaceSubsurfaceSourceTextureShaderName);

        public const string ScreenSpaceSubsurfaceBaseColorName = "ScreenSpaceSubsurfaceBaseColor";

        public const string ScreenSpaceSubsurfaceBaseColorTextureShaderName = "_BurtScreenSpaceSubsurfaceBaseColorTexture";

        public static readonly int ScreenSpaceSubsurfaceBaseColorTextureId = Shader.PropertyToID(ScreenSpaceSubsurfaceBaseColorTextureShaderName);

        public const string ScreenSpaceSubsurfaceEmissionName = "ScreenSpaceSubsurfaceEmission";

        public const string ScreenSpaceSubsurfaceEmissionTextureShaderName = "_BurtScreenSpaceSubsurfaceEmissionTexture";

        public static readonly int ScreenSpaceSubsurfaceEmissionTextureId = Shader.PropertyToID(ScreenSpaceSubsurfaceEmissionTextureShaderName);

        public const string ScreenSpaceSubsurfaceSetupName = "ScreenSpaceSubsurfaceSetup";

        public const string ScreenSpaceSubsurfaceSetupTextureShaderName = "_BurtScreenSpaceSubsurfaceSetupTexture";

        public static readonly int ScreenSpaceSubsurfaceSetupTextureId = Shader.PropertyToID(ScreenSpaceSubsurfaceSetupTextureShaderName);

        public const string ScreenSpaceSubsurfaceProfileIDAndTypeName = "ScreenSpaceSubsurfaceProfileIDAndType";

        public const string ScreenSpaceSubsurfaceProfileIDAndTypeTextureShaderName = "_BurtScreenSpaceSubsurfaceProfileIDAndTypeTexture";

        public static readonly int ScreenSpaceSubsurfaceProfileIDAndTypeTextureId = Shader.PropertyToID(ScreenSpaceSubsurfaceProfileIDAndTypeTextureShaderName);

        public const string ScreenSpaceSubsurfaceMaskName = "ScreenSpaceSubsurfaceMask";

        public const string ScreenSpaceSubsurfaceMaskTextureShaderName = "_BurtScreenSpaceSubsurfaceMaskTexture";

        public static readonly int ScreenSpaceSubsurfaceMaskTextureId = Shader.PropertyToID(ScreenSpaceSubsurfaceMaskTextureShaderName);

        public const string ScreenSpaceSubsurfaceTempName = "ScreenSpaceSubsurfaceTemp";

        public const string ScreenSpaceSubsurfaceTempTextureShaderName = "_BurtScreenSpaceSubsurfaceTempTexture";

        public static readonly int ScreenSpaceSubsurfaceTempTextureId = Shader.PropertyToID(ScreenSpaceSubsurfaceTempTextureShaderName);

        public const string ScreenSpaceSubsurfaceBlurName = "ScreenSpaceSubsurfaceBlur";

        public const string ScreenSpaceSubsurfaceBlurTextureShaderName = "_BurtScreenSpaceSubsurfaceBlurTexture";

        public static readonly int ScreenSpaceSubsurfaceBlurTextureId = Shader.PropertyToID(ScreenSpaceSubsurfaceBlurTextureShaderName);

        public const string ScreenSpaceSubsurfaceCombineName = "ScreenSpaceSubsurfaceCombine";

        public const string ScreenSpaceSubsurfaceCombineTextureShaderName = "_BurtScreenSpaceSubsurfaceCombineTexture";

        public static readonly int ScreenSpaceSubsurfaceCombineTextureId = Shader.PropertyToID(ScreenSpaceSubsurfaceCombineTextureShaderName);

        public const string ScreenSpaceSubsurfacePersistentHistoryName = "ScreenSpaceSubsurfacePersistentHistory";

        public const string ScreenSpaceSubsurfaceVelocityName = "ScreenSpaceSubsurfaceVelocity";

        public const string ScreenSpaceSubsurfaceVelocityTextureShaderName = "_BurtScreenSpaceSubsurfaceVelocityTexture";

        public static readonly int ScreenSpaceSubsurfaceVelocityTextureId = Shader.PropertyToID(ScreenSpaceSubsurfaceVelocityTextureShaderName);

        public const string FurBlurPropertyName = "FurBlurProperty";

        public const string FurBlurPropertyTextureShaderName = "_BurtFurBlurPropertyTexture";

        public static readonly int FurBlurPropertyTextureId = Shader.PropertyToID(FurBlurPropertyTextureShaderName);

        public const string FurBlurPropertyTempName = "FurBlurPropertyTemp";

        public const string FurBlurPropertyTempTextureShaderName = "_BurtFurBlurPropertyTempTexture";

        public static readonly int FurBlurPropertyTempTextureId = Shader.PropertyToID(FurBlurPropertyTempTextureShaderName);

        public const string FurBlurColorName = "FurBlurColor";

        public const string FurBlurColorTextureShaderName = "_BurtFurBlurColorTexture";

        public static readonly int FurBlurColorTextureId = Shader.PropertyToID(FurBlurColorTextureShaderName);

        public const string FurBlurTemporalName = "FurBlurTemporal";

        public const string FurBlurTemporalTextureShaderName = "_BurtFurBlurTemporalTexture";

        public static readonly int FurBlurTemporalTextureId = Shader.PropertyToID(FurBlurTemporalTextureShaderName);

        public const string FurBlurVelocityName = "FurBlurVelocity";

        public const string FurBlurVelocityTextureShaderName = "_BurtFurBlurVelocityTexture";

        public static readonly int FurBlurVelocityTextureId = Shader.PropertyToID(FurBlurVelocityTextureShaderName);

        public const string FurBlurHistoryName = "FurBlurHistory";

        public const string FurBlurPersistentHistoryName = "FurBlurPersistentHistory";

        public const string FurBlurHistoryTextureShaderName = "_BurtFurBlurHistoryTexture";

        public static readonly int FurBlurHistoryTextureId = Shader.PropertyToID(FurBlurHistoryTextureShaderName);

        public const string FurBlurArgsBufferName = "FurBlurArgsBuffer";

        public const string FurBlurTileDataBufferName = "FurBlurTileDataBuffer";

        public const string ScreenSpaceSubsurfaceBurleyArgsBufferName = "ScreenSpaceSubsurfaceBurleyArgsBuffer";

        public const string ScreenSpaceSubsurfaceBurleyGroupBufferName = "ScreenSpaceSubsurfaceBurleyGroupBuffer";

        public const string ScreenSpaceSubsurfaceSeparableArgsBufferName = "ScreenSpaceSubsurfaceSeparableArgsBuffer";

        public const string ScreenSpaceSubsurfaceSeparableGroupBufferName = "ScreenSpaceSubsurfaceSeparableGroupBuffer";

        public const string BurtGIScreenProbeIndirectArgsBufferName = "BurtGIScreenProbeIndirectArgsBuffer";

        public const string BurtGIScreenProbeIntegrateTileIndirectArgsBufferName = "BurtGIScreenProbeIntegrateTileIndirectArgsBuffer";

        public const string BurtGIScreenProbeIntegrateTileIndirectArgsBufferShaderName = "_BurtGIScreenProbeIntegrateTileIndirectArgsBuffer";

        public static readonly int BurtGIScreenProbeIntegrateTileIndirectArgsBufferId = Shader.PropertyToID(BurtGIScreenProbeIntegrateTileIndirectArgsBufferShaderName);

        public const string BurtGIScreenProbeIntegrateTileDataDiffuseBufferName = "BurtGIScreenProbeIntegrateTileDataDiffuseBuffer";

        public const string BurtGIScreenProbeIntegrateTileDataDiffuseBufferShaderName = "_BurtGIScreenProbeIntegrateTileDataDiffuseBuffer";

        public static readonly int BurtGIScreenProbeIntegrateTileDataDiffuseBufferId = Shader.PropertyToID(BurtGIScreenProbeIntegrateTileDataDiffuseBufferShaderName);

        public const string BurtGIScreenProbeIntegrateTileDataAllBufferName = "BurtGIScreenProbeIntegrateTileDataAllBuffer";

        public const string BurtGIScreenProbeIntegrateTileDataAllBufferShaderName = "_BurtGIScreenProbeIntegrateTileDataAllBuffer";

        public static readonly int BurtGIScreenProbeIntegrateTileDataAllBufferId = Shader.PropertyToID(BurtGIScreenProbeIntegrateTileDataAllBufferShaderName);

        public const string BurtGIScreenProbeTraceCompactTexelCountBufferName = "BurtGIScreenProbeTraceCompactTexelCountBuffer";

        public const string BurtGIScreenProbeTraceCompactTexelDataBufferName = "BurtGIScreenProbeTraceCompactTexelDataBuffer";

        public const string BurtGIScreenProbeTraceCompactIndirectArgsBufferName = "BurtGIScreenProbeTraceCompactIndirectArgsBuffer";

        public const string BurtGIScreenProbeTraceCompactThreadCountXBufferName = "BurtGIScreenProbeTraceCompactThreadCountXBuffer";

        public const string BurtGIScreenProbeAdaptiveProbeNumBufferName = "BurtGIScreenProbeAdaptiveProbeNumBuffer";

        public const string BurtGIScreenProbeAdaptiveProbeDataBufferName = "BurtGIScreenProbeAdaptiveProbeDataBuffer";

        public const string BurtGIRadianceCacheClipMapProbeAllocatorBufferName = "BurtGIRadianceCacheClipMapProbeAllocatorBuffer";

        public const string BurtGIRadianceCacheClipMapProbeAllocatorBufferShaderName = "_BurtGIRadianceCacheClipMapProbeAllocatorBuffer";

        public static readonly int BurtGIRadianceCacheClipMapProbeAllocatorBufferId = Shader.PropertyToID(BurtGIRadianceCacheClipMapProbeAllocatorBufferShaderName);

        public const string BurtGIRadianceCacheClipMapProbeFreeListAllocatorBufferName = "BurtGIRadianceCacheClipMapProbeFreeListAllocatorBuffer";

        public const string BurtGIRadianceCacheClipMapProbeFreeListAllocatorBufferShaderName = "_BurtGIRadianceCacheClipMapProbeFreeListAllocatorBuffer";

        public static readonly int BurtGIRadianceCacheClipMapProbeFreeListAllocatorBufferId = Shader.PropertyToID(BurtGIRadianceCacheClipMapProbeFreeListAllocatorBufferShaderName);

        public const string BurtGIRadianceCacheClipMapProbeFreeListBufferName = "BurtGIRadianceCacheClipMapProbeFreeListBuffer";

        public const string BurtGIRadianceCacheClipMapProbeFreeListBufferShaderName = "_BurtGIRadianceCacheClipMapProbeFreeListBuffer";

        public static readonly int BurtGIRadianceCacheClipMapProbeFreeListBufferId = Shader.PropertyToID(BurtGIRadianceCacheClipMapProbeFreeListBufferShaderName);

        public const string BurtGIRadianceCacheClipMapProbeLastUsedFrameBufferName = "BurtGIRadianceCacheClipMapProbeLastUsedFrameBuffer";

        public const string BurtGIRadianceCacheClipMapProbeLastUsedFrameBufferShaderName = "_BurtGIRadianceCacheClipMapProbeLastUsedFrameBuffer";

        public static readonly int BurtGIRadianceCacheClipMapProbeLastUsedFrameBufferId = Shader.PropertyToID(BurtGIRadianceCacheClipMapProbeLastUsedFrameBufferShaderName);

        public const string BurtGIRadianceCacheClipMapProbeLastTracedFrameBufferName = "BurtGIRadianceCacheClipMapProbeLastTracedFrameBuffer";

        public const string BurtGIRadianceCacheClipMapProbeLastTracedFrameBufferShaderName = "_BurtGIRadianceCacheClipMapProbeLastTracedFrameBuffer";

        public static readonly int BurtGIRadianceCacheClipMapProbeLastTracedFrameBufferId = Shader.PropertyToID(BurtGIRadianceCacheClipMapProbeLastTracedFrameBufferShaderName);

        public const string BurtGIRadianceCacheClipMapProbeWorldOffsetBufferName = "BurtGIRadianceCacheClipMapProbeWorldOffsetBuffer";

        public const string BurtGIRadianceCacheClipMapProbeWorldOffsetBufferShaderName = "_BurtGIRadianceCacheClipMapProbeWorldOffsetBuffer";

        public static readonly int BurtGIRadianceCacheClipMapProbeWorldOffsetBufferId = Shader.PropertyToID(BurtGIRadianceCacheClipMapProbeWorldOffsetBufferShaderName);

        public const string BurtGIRadianceCacheClipMapProbeTraceDataBufferName = "BurtGIRadianceCacheClipMapProbeTraceDataBuffer";

        public const string BurtGIRadianceCacheClipMapProbeTraceDataBufferShaderName = "_BurtGIRadianceCacheClipMapProbeTraceDataBuffer";

        public static readonly int BurtGIRadianceCacheClipMapProbeTraceDataBufferId = Shader.PropertyToID(BurtGIRadianceCacheClipMapProbeTraceDataBufferShaderName);

        public const string BurtGIRadianceCacheClipMapProbeTraceAllocatorBufferName = "BurtGIRadianceCacheClipMapProbeTraceAllocatorBuffer";

        public const string BurtGIRadianceCacheClipMapProbeTraceAllocatorBufferShaderName = "_BurtGIRadianceCacheClipMapProbeTraceAllocatorBuffer";

        public static readonly int BurtGIRadianceCacheClipMapProbeTraceAllocatorBufferId = Shader.PropertyToID(BurtGIRadianceCacheClipMapProbeTraceAllocatorBufferShaderName);

        public const string BurtGIRadianceCacheClipMapPriorityHistogramBufferName = "BurtGIRadianceCacheClipMapPriorityHistogramBuffer";

        public const string BurtGIRadianceCacheClipMapPriorityHistogramBufferShaderName = "_BurtGIRadianceCacheClipMapPriorityHistogramBuffer";

        public static readonly int BurtGIRadianceCacheClipMapPriorityHistogramBufferId = Shader.PropertyToID(BurtGIRadianceCacheClipMapPriorityHistogramBufferShaderName);

        public const string BurtGIRadianceCacheClipMapMaxUpdateBucketBufferName = "BurtGIRadianceCacheClipMapMaxUpdateBucketBuffer";

        public const string BurtGIRadianceCacheClipMapMaxUpdateBucketBufferShaderName = "_BurtGIRadianceCacheClipMapMaxUpdateBucketBuffer";

        public static readonly int BurtGIRadianceCacheClipMapMaxUpdateBucketBufferId = Shader.PropertyToID(BurtGIRadianceCacheClipMapMaxUpdateBucketBufferShaderName);

        public const string BurtGIRadianceCacheClipMapMaxTracesFromMaxUpdateBucketBufferName = "BurtGIRadianceCacheClipMapMaxTracesFromMaxUpdateBucketBuffer";

        public const string BurtGIRadianceCacheClipMapMaxTracesFromMaxUpdateBucketBufferShaderName = "_BurtGIRadianceCacheClipMapMaxTracesFromMaxUpdateBucketBuffer";

        public static readonly int BurtGIRadianceCacheClipMapMaxTracesFromMaxUpdateBucketBufferId = Shader.PropertyToID(BurtGIRadianceCacheClipMapMaxTracesFromMaxUpdateBucketBufferShaderName);

        public const string BurtGIRadianceCacheClipMapProbesToUpdateTraceCostBufferName = "BurtGIRadianceCacheClipMapProbesToUpdateTraceCostBuffer";

        public const string BurtGIRadianceCacheClipMapProbesToUpdateTraceCostBufferShaderName = "_BurtGIRadianceCacheClipMapProbesToUpdateTraceCostBuffer";

        public static readonly int BurtGIRadianceCacheClipMapProbesToUpdateTraceCostBufferId = Shader.PropertyToID(BurtGIRadianceCacheClipMapProbesToUpdateTraceCostBufferShaderName);

        public const string BurtGIRadianceCacheClipMapRadianceProbePDFBufferName = "BurtGIRadianceCacheClipMapRadianceProbePDFBuffer";

        public const string BurtGIRadianceCacheClipMapRadianceProbePDFBufferShaderName = "_BurtGIRadianceCacheClipMapRadianceProbePDFBuffer";

        public static readonly int BurtGIRadianceCacheClipMapRadianceProbePDFBufferId = Shader.PropertyToID(BurtGIRadianceCacheClipMapRadianceProbePDFBufferShaderName);

        public const string BurtGIRadianceCacheClipMapClearProbePDFsIndirectArgsBufferName = "BurtGIRadianceCacheClipMapClearProbePDFsIndirectArgsBuffer";

        public const string BurtGIRadianceCacheClipMapClearProbePDFsIndirectArgsBufferShaderName = "_BurtGIRadianceCacheClipMapClearProbePDFsIndirectArgsBuffer";

        public static readonly int BurtGIRadianceCacheClipMapClearProbePDFsIndirectArgsBufferId = Shader.PropertyToID(BurtGIRadianceCacheClipMapClearProbePDFsIndirectArgsBufferShaderName);

        public const string BurtGIRadianceCacheClipMapGenerateProbeTraceTilesIndirectArgsBufferName = "BurtGIRadianceCacheClipMapGenerateProbeTraceTilesIndirectArgsBuffer";

        public const string BurtGIRadianceCacheClipMapGenerateProbeTraceTilesIndirectArgsBufferShaderName = "_BurtGIRadianceCacheClipMapGenerateProbeTraceTilesIndirectArgsBuffer";

        public static readonly int BurtGIRadianceCacheClipMapGenerateProbeTraceTilesIndirectArgsBufferId = Shader.PropertyToID(BurtGIRadianceCacheClipMapGenerateProbeTraceTilesIndirectArgsBufferShaderName);

        public const string BurtGIRadianceCacheClipMapProbeTraceTileAllocatorBufferName = "BurtGIRadianceCacheClipMapProbeTraceTileAllocatorBuffer";

        public const string BurtGIRadianceCacheClipMapProbeTraceTileAllocatorBufferShaderName = "_BurtGIRadianceCacheClipMapProbeTraceTileAllocatorBuffer";

        public static readonly int BurtGIRadianceCacheClipMapProbeTraceTileAllocatorBufferId = Shader.PropertyToID(BurtGIRadianceCacheClipMapProbeTraceTileAllocatorBufferShaderName);

        public const string BurtGIRadianceCacheClipMapFilterProbesIndirectArgsBufferName = "BurtGIRadianceCacheClipMapFilterProbesIndirectArgsBuffer";

        public const string BurtGIRadianceCacheClipMapFilterProbesIndirectArgsBufferShaderName = "_BurtGIRadianceCacheClipMapFilterProbesIndirectArgsBuffer";

        public static readonly int BurtGIRadianceCacheClipMapFilterProbesIndirectArgsBufferId = Shader.PropertyToID(BurtGIRadianceCacheClipMapFilterProbesIndirectArgsBufferShaderName);

        public const string BurtGIRadianceCacheClipMapPrepareProbeOcclusionIndirectArgsBufferName = "BurtGIRadianceCacheClipMapPrepareProbeOcclusionIndirectArgsBuffer";

        public const string BurtGIRadianceCacheClipMapPrepareProbeOcclusionIndirectArgsBufferShaderName = "_BurtGIRadianceCacheClipMapPrepareProbeOcclusionIndirectArgsBuffer";

        public static readonly int BurtGIRadianceCacheClipMapPrepareProbeOcclusionIndirectArgsBufferId = Shader.PropertyToID(BurtGIRadianceCacheClipMapPrepareProbeOcclusionIndirectArgsBufferShaderName);

        public const string BurtGIRadianceCacheClipMapFixupProbeBordersIndirectArgsBufferName = "BurtGIRadianceCacheClipMapFixupProbeBordersIndirectArgsBuffer";

        public const string BurtGIRadianceCacheClipMapFixupProbeBordersIndirectArgsBufferShaderName = "_BurtGIRadianceCacheClipMapFixupProbeBordersIndirectArgsBuffer";

        public static readonly int BurtGIRadianceCacheClipMapFixupProbeBordersIndirectArgsBufferId = Shader.PropertyToID(BurtGIRadianceCacheClipMapFixupProbeBordersIndirectArgsBufferShaderName);

        public const string BurtGIRadianceCacheClipMapTraceProbesIndirectArgsBufferName = "BurtGIRadianceCacheClipMapTraceProbesIndirectArgsBuffer";

        public const string BurtGIRadianceCacheClipMapTraceProbesIndirectArgsBufferShaderName = "_BurtGIRadianceCacheClipMapTraceProbesIndirectArgsBuffer";

        public static readonly int BurtGIRadianceCacheClipMapTraceProbesIndirectArgsBufferId = Shader.PropertyToID(BurtGIRadianceCacheClipMapTraceProbesIndirectArgsBufferShaderName);

        public const string BurtGIRadianceCacheClipMapSortProbeTraceTilesIndirectArgsBufferName = "BurtGIRadianceCacheClipMapSortProbeTraceTilesIndirectArgsBuffer";

        public const string BurtGIRadianceCacheClipMapSortProbeTraceTilesIndirectArgsBufferShaderName = "_BurtGIRadianceCacheClipMapSortProbeTraceTilesIndirectArgsBuffer";

        public static readonly int BurtGIRadianceCacheClipMapSortProbeTraceTilesIndirectArgsBufferId = Shader.PropertyToID(BurtGIRadianceCacheClipMapSortProbeTraceTilesIndirectArgsBufferShaderName);

        public const string BurtGIRadianceCacheClipMapRadianceCacheHardwareRayTracingIndirectArgsBufferName = "BurtGIRadianceCacheClipMapRadianceCacheHardwareRayTracingIndirectArgsBuffer";

        public const string BurtGIRadianceCacheClipMapRadianceCacheHardwareRayTracingIndirectArgsBufferShaderName = "_BurtGIRadianceCacheClipMapRadianceCacheHardwareRayTracingIndirectArgsBuffer";

        public static readonly int BurtGIRadianceCacheClipMapRadianceCacheHardwareRayTracingIndirectArgsBufferId = Shader.PropertyToID(BurtGIRadianceCacheClipMapRadianceCacheHardwareRayTracingIndirectArgsBufferShaderName);

        public const string BurtGIRadianceCacheClipMapHardwareRayTracingRayAllocatorBufferName = "BurtGIRadianceCacheClipMapHardwareRayTracingRayAllocatorBuffer";

        public const string BurtGIRadianceCacheClipMapHardwareRayTracingRayAllocatorBufferShaderName = "_BurtGIRadianceCacheClipMapHardwareRayTracingRayAllocatorBuffer";

        public static readonly int BurtGIRadianceCacheClipMapHardwareRayTracingRayAllocatorBufferId = Shader.PropertyToID(BurtGIRadianceCacheClipMapHardwareRayTracingRayAllocatorBufferShaderName);

        public const string BurtGIRadianceCacheClipMapProbeTraceTileDataBufferName = "BurtGIRadianceCacheClipMapProbeTraceTileDataBuffer";

        public const string BurtGIRadianceCacheClipMapProbeTraceTileDataBufferShaderName = "_BurtGIRadianceCacheClipMapProbeTraceTileDataBuffer";

        public static readonly int BurtGIRadianceCacheClipMapProbeTraceTileDataBufferId = Shader.PropertyToID(BurtGIRadianceCacheClipMapProbeTraceTileDataBufferShaderName);

        public const string BurtGIRadianceCacheClipMapSortedProbeTraceTileDataBufferName = "BurtGIRadianceCacheClipMapSortedProbeTraceTileDataBuffer";

        public const string BurtGIRadianceCacheClipMapSortedProbeTraceTileDataBufferShaderName = "_BurtGIRadianceCacheClipMapSortedProbeTraceTileDataBuffer";

        public static readonly int BurtGIRadianceCacheClipMapSortedProbeTraceTileDataBufferId = Shader.PropertyToID(BurtGIRadianceCacheClipMapSortedProbeTraceTileDataBufferShaderName);

        public const string BurtGIRadianceCacheHashGridValueBufferName = "BurtGIRadianceCacheHashGridValueBuffer";

        public const string BurtGIRadianceCacheHashGridValueBufferShaderName = "_BurtGIRadianceCacheHashGridValueBuffer";

        public static readonly int BurtGIRadianceCacheHashGridValueBufferId = Shader.PropertyToID(BurtGIRadianceCacheHashGridValueBufferShaderName);

        public const string BurtGIRadianceCacheHashGridTileBufferName = "BurtGIRadianceCacheHashGridTileBuffer";

        public const string BurtGIRadianceCacheHashGridTileBufferShaderName = "_BurtGIRadianceCacheHashGridTileBuffer";

        public static readonly int BurtGIRadianceCacheHashGridTileBufferId = Shader.PropertyToID(BurtGIRadianceCacheHashGridTileBufferShaderName);

        public const string BurtGIRadianceCacheHashGridCountBufferName = "BurtGIRadianceCacheHashGridCountBuffer";

        public const string BurtGIRadianceCacheHashGridCountBufferShaderName = "_BurtGIRadianceCacheHashGridCountBuffer";

        public static readonly int BurtGIRadianceCacheHashGridCountBufferId = Shader.PropertyToID(BurtGIRadianceCacheHashGridCountBufferShaderName);

        public const string BurtGIRadianceCacheHashGridVisibilityCellQueryBufferName = "BurtGIRadianceCacheHashGridVisibilityCellQueryBuffer";

        public const string BurtGIRadianceCacheHashGridVisibilityCellQueryBufferShaderName = "_BurtGIRadianceCacheHashGridVisibilityCellQueryBuffer";

        public static readonly int BurtGIRadianceCacheHashGridVisibilityCellQueryBufferId = Shader.PropertyToID(BurtGIRadianceCacheHashGridVisibilityCellQueryBufferShaderName);

        public const string BurtGIRadianceCacheHashGridUpdateCellValueBufferName = "BurtGIRadianceCacheHashGridUpdateCellValueBuffer";

        public const string BurtGIRadianceCacheHashGridUpdateCellValueBufferShaderName = "_BurtGIRadianceCacheHashGridUpdateCellValueBuffer";

        public static readonly int BurtGIRadianceCacheHashGridUpdateCellValueBufferId = Shader.PropertyToID(BurtGIRadianceCacheHashGridUpdateCellValueBufferShaderName);

        public const string BurtGIRadianceCacheHashGridUpdateTileBufferName = "BurtGIRadianceCacheHashGridUpdateTileBuffer";

        public const string BurtGIRadianceCacheHashGridUpdateTileBufferShaderName = "_BurtGIRadianceCacheHashGridUpdateTileBuffer";

        public static readonly int BurtGIRadianceCacheHashGridUpdateTileBufferId = Shader.PropertyToID(BurtGIRadianceCacheHashGridUpdateTileBufferShaderName);

        public const string BurtGIRadianceCacheHashGridUpdateTilesIndirectArgsBufferName = "BurtGIRadianceCacheHashGridUpdateTilesIndirectArgsBuffer";

        public const string BurtGIRadianceCacheHashGridUpdateTilesIndirectArgsBufferShaderName = "_BurtGIRadianceCacheHashGridUpdateTilesIndirectArgsBuffer";

        public static readonly int BurtGIRadianceCacheHashGridUpdateTilesIndirectArgsBufferId = Shader.PropertyToID(BurtGIRadianceCacheHashGridUpdateTilesIndirectArgsBufferShaderName);

        public const string BurtGIRadianceCacheHashGridUpdateTilesGroupCountXBufferName = "BurtGIRadianceCacheHashGridUpdateTilesGroupCountXBuffer";

        public const string BurtGIRadianceCacheHashGridUpdateTilesGroupCountXBufferShaderName = "_BurtGIRadianceCacheHashGridUpdateTilesGroupCountXBuffer";

        public static readonly int BurtGIRadianceCacheHashGridUpdateTilesGroupCountXBufferId = Shader.PropertyToID(BurtGIRadianceCacheHashGridUpdateTilesGroupCountXBufferShaderName);

        public const string BurtGIRadianceCacheHashGridDebugCellBufferName = "BurtGIRadianceCacheHashGridDebugCellBuffer";

        public const string BurtGIRadianceCacheHashGridDebugCellBufferShaderName = "_BurtGIRadianceCacheHashGridDebugCellBuffer";

        public static readonly int BurtGIRadianceCacheHashGridDebugCellBufferId = Shader.PropertyToID(BurtGIRadianceCacheHashGridDebugCellBufferShaderName);

        public const string BurtGIRadianceCacheHashGridDebugDrawArgsBufferName = "BurtGIRadianceCacheHashGridDebugDrawArgsBuffer";

        public const string BurtGIRadianceCacheHashGridDebugDrawArgsBufferShaderName = "_BurtGIRadianceCacheHashGridDebugDrawArgsBuffer";

        public static readonly int BurtGIRadianceCacheHashGridDebugDrawArgsBufferId = Shader.PropertyToID(BurtGIRadianceCacheHashGridDebugDrawArgsBufferShaderName);

        public const string MainLightShadowMapName = "MainLightShadowMap"; // 定义主光阴影图在 RenderGraph 里的统一资源名，后续阴影绘制和光照采样都通过它建立依赖。

        public const string MainLightShadowMapShaderName = "_BurtMainLightShadowMap"; // 定义主光阴影图暴露给 shader 的全局纹理名称，后续 Lit shader 会用这个名字采样阴影。

        public static readonly int MainLightShadowMapId = Shader.PropertyToID(MainLightShadowMapShaderName); // 把主光阴影图 shader 名称转换成整数 ID，让 CommandBuffer 申请、释放和绑定同一个临时 RT。

        public const string AdditionalLightShadowAtlasName = "AdditionalLightShadowAtlas";

        public const string AdditionalLightShadowAtlasShaderName = "_BurtAdditionalLightShadowAtlas";

        public static readonly int AdditionalLightShadowAtlasId = Shader.PropertyToID(AdditionalLightShadowAtlasShaderName);

        public const string PerObjectShadowAtlasName = "PerObjectShadowAtlas";

        public const string PerObjectShadowAtlasShaderName = "_BurtPerObjectShadowAtlas";

        public static readonly int PerObjectShadowAtlasId = Shader.PropertyToID(PerObjectShadowAtlasShaderName);

        public const string LightingGlobalsName = "LightingGlobals"; // 定义灯光全局状态的逻辑资源名，用来让 Setup Lighting 和 Shading Pass 建立依赖。

        public const string ShadowGlobalsName = "ShadowGlobals"; // 定义阴影全局状态的逻辑资源名，用来描述 shadow matrix、shadow strength 等 shader 全局变量。

        public const string BurtGIApplyIndirectGlobalsName = "BurtGIApplyIndirectGlobals";

        public const string AdditionalLightBufferName = "AdditionalLightBuffer"; // Future structured buffer for multi-light data when tiled/cluster lighting replaces global arrays.

        public const string TileLightCountBufferName = "TileLightCountBuffer"; // Per-tile light count buffer used by tiled deferred lighting.

        public const string TileLightListBufferName = "TileLightListBuffer"; // Per-tile light index list buffer used by tiled deferred lighting.

        public const string TileLightOffsetBufferName = "TileLightOffsetBuffer"; // Per-tile offset/count buffer used by tiled deferred lighting.

        public const string ClusterLightCountBufferName = "ClusterLightCountBuffer"; // Per-cluster light count buffer used by clustered deferred lighting.

        public const string ClusterLightListBufferName = "ClusterLightListBuffer"; // Per-cluster light index list buffer used by clustered deferred lighting.

        public const string ClusterLightOffsetBufferName = "ClusterLightOffsetBuffer"; // Per-cluster offset/count buffer used by clustered deferred lighting.

        public const string PunctualTileIdBufferName = "PunctualTileIdBuffer"; // Compact packed XY tile ids consumed by deferred punctual tile draws.

        private const string UnnamedRenderTargetName = "UnnamedRenderTarget"; // 定义空资源名的兜底名称，避免 Dictionary 接收 null 或空字符串。

        private const string UnnamedBufferName = "UnnamedBuffer"; // Fallback logical buffer name used when a declaration passes null or empty.

        private readonly Dictionary<string, BurtRenderTargetHandle> renderTargets = new Dictionary<string, BurtRenderTargetHandle>(); // 创建渲染目标字典，用资源名映射到渲染目标句柄。

        private readonly Dictionary<string, BurtRenderResourceId> renderTargetIds = new Dictionary<string, BurtRenderResourceId>();

        private readonly HashSet<string> externalRenderTargets = new HashSet<string>(); // 记录由相机或外部系统提供的资源，Read-before-Write 校验会把它们视为已有生产者。

        private readonly Dictionary<string, BurtRenderTextureDescriptor> renderTargetDescriptors = new Dictionary<string, BurtRenderTextureDescriptor>();

        private readonly Dictionary<string, RenderTexture> allocatedRenderTextures = new Dictionary<string, RenderTexture>();

        private readonly Dictionary<BurtRenderTexturePoolKey, Stack<RenderTexture>> availableRenderTexturePool = new Dictionary<BurtRenderTexturePoolKey, Stack<RenderTexture>>();

        private readonly Dictionary<string, BurtRenderBufferHandle> buffers = new Dictionary<string, BurtRenderBufferHandle>(); // Logical buffer registry for future tiled/cluster resources.

        private readonly Dictionary<string, BurtRenderResourceId> bufferIds = new Dictionary<string, BurtRenderResourceId>();

        private readonly Dictionary<string, BurtRenderBufferDescriptor> bufferDescriptors = new Dictionary<string, BurtRenderBufferDescriptor>(); // Allocation descriptors for graph-owned GPU buffers.

        private readonly HashSet<string> externalBuffers = new HashSet<string>(); // Buffers imported from outside the graph are valid read sources.

        private readonly List<GraphicsBuffer> deferredBufferReleases = new List<GraphicsBuffer>(); // Buffers queued by release passes until ScriptableRenderContext.Submit has consumed draw commands.

        private readonly Dictionary<BurtRenderBufferPoolKey, Stack<GraphicsBuffer>> availableBufferPool = new Dictionary<BurtRenderBufferPoolKey, Stack<GraphicsBuffer>>();
        private readonly List<string> bufferReleaseScratch = new List<string>();
        private readonly List<string> renderTextureReleaseScratch = new List<string>();

        private int nextRenderTargetIndex;
        private int nextBufferIndex;
        private uint nextResourceVersion = 1;
        private int nextPhysicalRenderTextureIndex = 1;

        public IEnumerable<string> RenderTargetNames => renderTargets.Keys; // Exposes registered RT names for debug dumps without exposing the dictionary.

        public IEnumerable<string> ExternalRenderTargetNames => externalRenderTargets; // Exposes external RT names for debug dumps.

        public IEnumerable<string> BufferNames => buffers.Keys; // Exposes registered logical buffer names for debug dumps.

        public IEnumerable<string> ExternalBufferNames => externalBuffers; // Exposes external buffer names for debug dumps.

        public static string GetBloomDownsampleName(int mipIndex)
        {
            return BloomDownsampleNames[Mathf.Clamp(mipIndex, 0, BloomPyramidCount - 1)];
        }

        public static string GetBloomGaussianHorizontalName(int mipIndex)
        {
            return BloomGaussianHorizontalNames[Mathf.Clamp(mipIndex, 0, BloomPyramidCount - 1)];
        }

        public static string GetBloomGaussianVerticalName(int mipIndex)
        {
            return BloomGaussianVerticalNames[Mathf.Clamp(mipIndex, 0, BloomPyramidCount - 1)];
        }

        public static int GetBloomDownsampleTextureId(int mipIndex)
        {
            return BloomDownsampleTextureIds[Mathf.Clamp(mipIndex, 0, BloomPyramidCount - 1)];
        }

        public static int GetBloomGaussianHorizontalTextureId(int mipIndex)
        {
            return BloomGaussianHorizontalTextureIds[Mathf.Clamp(mipIndex, 0, BloomPyramidCount - 1)];
        }

        public static int GetBloomGaussianVerticalTextureId(int mipIndex)
        {
            return BloomGaussianVerticalTextureIds[Mathf.Clamp(mipIndex, 0, BloomPyramidCount - 1)];
        }

        private static string[] CreateBloomResourceNames(string prefix)
        {
            var names = new string[BloomPyramidCount];
            for (var mipIndex = 0; mipIndex < names.Length; mipIndex++)
            {
                names[mipIndex] = prefix + mipIndex;
            }

            return names;
        }

        private static int[] CreateBloomTextureIds(string prefix)
        {
            var ids = new int[BloomPyramidCount];
            for (var mipIndex = 0; mipIndex < ids.Length; mipIndex++)
            {
                ids[mipIndex] = Shader.PropertyToID(prefix + mipIndex);
            }

            return ids;
        }

        public bool HasPendingBufferReleases
        {
            get
            {
                return deferredBufferReleases.Count > 0;
            }
        }

        public void Clear() // Clears graph resources before assembling the next RenderGraph request.
        {
            RecycleAllInternalRenderTextures();

            RecycleAllInternalBuffers(); // Preserve descriptor-compatible buffers for later requests instead of destroying the pool every camera.

            renderTargets.Clear(); // Clear render targets registered by the previous request.

            renderTargetIds.Clear();

            externalRenderTargets.Clear(); // Clear external render target markers for the next request.

            renderTargetDescriptors.Clear();

            allocatedRenderTextures.Clear();

            buffers.Clear(); // Clear logical buffer registrations alongside render targets.

            bufferIds.Clear();

            nextRenderTargetIndex = 0;
            nextBufferIndex = 0;

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

            ReleaseInternalRenderTargetIfNeeded(safeName);
            renderTargetDescriptors.Remove(safeName);

            var resourceId = new BurtRenderResourceId(
                BurtRenderResourceType.RenderTarget,
                renderTargetIds.TryGetValue(safeName, out var previousId) ? previousId.Index : nextRenderTargetIndex++,
                NextResourceVersion());
            renderTargetIds[safeName] = resourceId;

            var handle = new BurtRenderTargetHandle(safeName, identifier, resourceId); // 把资源名和 Unity 渲染目标标识包装成 BurtRenderTargetHandle。

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

        public bool IsCurrent(BurtRenderTargetHandle handle)
        {
            return handle.IsValid &&
                handle.ResourceId.IsValid &&
                renderTargetIds.TryGetValue(NormalizeResourceName(handle.Name), out var currentId) &&
                currentId.Index == handle.ResourceId.Index &&
                currentId.Version == handle.ResourceId.Version;
        }

        public void SetRenderTargetDescriptor(
            string name,
            RenderTextureDescriptor descriptor,
            FilterMode filterMode = FilterMode.Bilinear,
            string debugName = null)
        {
            var safeName = NormalizeResourceName(name);
            if (externalRenderTargets.Contains(safeName))
            {
                return;
            }

            var allocationDescriptor = new BurtRenderTextureDescriptor(descriptor, filterMode, debugName);
            if (!allocationDescriptor.IsValid)
            {
                renderTargetDescriptors.Remove(safeName);
                return;
            }

            if (allocatedRenderTextures.TryGetValue(safeName, out var allocatedTexture) &&
                renderTargetDescriptors.TryGetValue(safeName, out var previousDescriptor) &&
                !new BurtRenderTexturePoolKey(previousDescriptor).Equals(new BurtRenderTexturePoolKey(allocationDescriptor)))
            {
                ReturnRenderTextureToPool(allocatedTexture, previousDescriptor);
                allocatedRenderTextures.Remove(safeName);
            }

            renderTargetDescriptors[safeName] = allocationDescriptor;
        }

        public BurtRenderTargetHandle AllocateRenderTarget(string name)
        {
            var safeName = NormalizeResourceName(name);
            if (externalRenderTargets.Contains(safeName) ||
                !renderTargetDescriptors.TryGetValue(safeName, out var allocationDescriptor) ||
                !allocationDescriptor.IsValid)
            {
                return GetRenderTarget(safeName);
            }

            if (allocatedRenderTextures.TryGetValue(safeName, out var currentTexture) && currentTexture != null)
            {
                return GetRenderTarget(safeName);
            }

            var texture = TryTakePooledRenderTexture(allocationDescriptor);
            if (texture == null)
            {
                texture = new RenderTexture(allocationDescriptor.Descriptor);
                // A pooled RenderTexture can back several non-overlapping logical graph resources.
                // Give the GPU object a stable physical-slot name before Create(): renaming an
                // already-created D3D11 resource does not update its RenderDoc debug name and can
                // otherwise leave a Fog scratch target labelled as an older Post Process target.
                texture.name = CreatePhysicalRenderTextureName(allocationDescriptor.Descriptor);
            }

            texture.filterMode = allocationDescriptor.FilterMode;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.hideFlags = HideFlags.HideAndDontSave;
            if (!texture.IsCreated())
            {
                texture.Create();
            }

            allocatedRenderTextures[safeName] = texture;
            var resourceId = renderTargetIds.TryGetValue(safeName, out var registeredId)
                ? registeredId
                : new BurtRenderResourceId(BurtRenderResourceType.RenderTarget, nextRenderTargetIndex++, NextResourceVersion());
            renderTargetIds[safeName] = resourceId;
            var handle = new BurtRenderTargetHandle(safeName, new RenderTargetIdentifier(texture), resourceId);
            renderTargets[safeName] = handle;
            externalRenderTargets.Remove(safeName);
            return handle;
        }

        public void ReleaseRenderTarget(string name)
        {
            var safeName = NormalizeResourceName(name);
            if (externalRenderTargets.Contains(safeName) ||
                !allocatedRenderTextures.TryGetValue(safeName, out var texture))
            {
                return;
            }

            if (renderTargetDescriptors.TryGetValue(safeName, out var descriptor) && descriptor.IsValid)
            {
                ReturnRenderTextureToPool(texture, descriptor);
            }
            else
            {
                ReleaseRenderTextureObject(texture);
            }

            allocatedRenderTextures.Remove(safeName);
        }

        public bool IsRenderTargetAllocated(string name)
        {
            return allocatedRenderTextures.TryGetValue(NormalizeResourceName(name), out var texture) && texture != null;
        }

        public bool TryGetAllocatedRenderTexture(string name, out RenderTexture texture)
        {
            return allocatedRenderTextures.TryGetValue(NormalizeResourceName(name), out texture) && texture != null;
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

            var resourceId = new BurtRenderResourceId(
                BurtRenderResourceType.Buffer,
                bufferIds.TryGetValue(safeName, out var previousId) ? previousId.Index : nextBufferIndex++,
                NextResourceVersion());
            bufferIds[safeName] = resourceId;
            var handle = new BurtRenderBufferHandle(safeName, externalBuffer, resourceId);
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

        public bool IsCurrent(BurtRenderBufferHandle handle)
        {
            return handle.IsValid &&
                handle.ResourceId.IsValid &&
                bufferIds.TryGetValue(NormalizeBufferName(handle.Name), out var currentId) &&
                currentId.Index == handle.ResourceId.Index &&
                currentId.Version == handle.ResourceId.Version;
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

            var buffer = TryTakePooledBuffer(descriptor);
            if (buffer == null)
            {
                buffer = new GraphicsBuffer(descriptor.Target, descriptor.Count, descriptor.Stride);
            }

            buffer.name = string.IsNullOrEmpty(descriptor.DebugName) ? safeName : descriptor.DebugName;

            var resourceId = bufferIds.TryGetValue(safeName, out var registeredId)
                ? registeredId
                : new BurtRenderResourceId(BurtRenderResourceType.Buffer, nextBufferIndex++, NextResourceVersion());
            bufferIds[safeName] = resourceId;
            var handle = new BurtRenderBufferHandle(safeName, buffer, resourceId);
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

            if (buffers.TryGetValue(safeName, out var allocatedHandle) &&
                allocatedHandle.HasBuffer &&
                bufferDescriptors.TryGetValue(safeName, out var descriptor) &&
                descriptor.IsValid)
            {
                ReturnBufferToPool(allocatedHandle.Buffer, descriptor);
            }
            else
            {
                QueueInternalBufferReleaseIfNeeded(safeName);
            }

            if (buffers.ContainsKey(safeName))
            {
                var resourceId = bufferIds.TryGetValue(safeName, out var registeredId)
                    ? registeredId
                    : BurtRenderResourceId.Invalid;
                buffers[safeName] = new BurtRenderBufferHandle(safeName, null, resourceId);
            }
        }

        private uint NextResourceVersion()
        {
            if (nextResourceVersion == 0)
            {
                nextResourceVersion = 1;
            }

            return nextResourceVersion++;
        }

        public void FlushDeferredBufferReleases()
        {
            for (var bufferIndex = 0; bufferIndex < deferredBufferReleases.Count; bufferIndex++)
            {
                ReleaseBufferObject(deferredBufferReleases[bufferIndex]);
            }

            deferredBufferReleases.Clear();
        }

        public void DisposeResources()
        {
            FlushDeferredBufferReleases();

            foreach (var pooledTextures in availableRenderTexturePool.Values)
            {
                while (pooledTextures.Count > 0)
                {
                    ReleaseRenderTextureObject(pooledTextures.Pop());
                }
            }

            availableRenderTexturePool.Clear();
            renderTextureReleaseScratch.Clear();
            foreach (var pair in allocatedRenderTextures)
            {
                if (!externalRenderTargets.Contains(pair.Key))
                {
                    ReleaseRenderTextureObject(pair.Value);
                    renderTextureReleaseScratch.Add(pair.Key);
                }
            }

            for (var textureIndex = 0; textureIndex < renderTextureReleaseScratch.Count; textureIndex++)
            {
                allocatedRenderTextures.Remove(renderTextureReleaseScratch[textureIndex]);
            }

            renderTextureReleaseScratch.Clear();

            foreach (var pooledBuffers in availableBufferPool.Values)
            {
                while (pooledBuffers.Count > 0)
                {
                    ReleaseBufferObject(pooledBuffers.Pop());
                }
            }

            availableBufferPool.Clear();

            bufferReleaseScratch.Clear();
            foreach (var pair in buffers)
            {
                if (externalBuffers.Contains(pair.Key) || !pair.Value.HasBuffer)
                {
                    continue;
                }

                ReleaseBufferObject(pair.Value.Buffer);
                bufferReleaseScratch.Add(pair.Key);
            }

            for (var bufferIndex = 0; bufferIndex < bufferReleaseScratch.Count; bufferIndex++)
            {
                var bufferName = bufferReleaseScratch[bufferIndex];
                var resourceId = bufferIds.TryGetValue(bufferName, out var registeredId)
                    ? registeredId
                    : BurtRenderResourceId.Invalid;
                buffers[bufferName] = new BurtRenderBufferHandle(bufferName, null, resourceId);
            }

            bufferReleaseScratch.Clear();
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

        public BurtRenderTargetHandle RegisterOpaqueCameraColorTexture()
        {
            return RegisterOpaqueCameraColor(new RenderTargetIdentifier(OpaqueCameraColorTextureId));
        }

        public BurtRenderTargetHandle RegisterOpaqueCameraColor(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(OpaqueCameraColorName, identifier);
        }

        public BurtRenderTargetHandle GetOpaqueCameraColor()
        {
            return GetRenderTarget(OpaqueCameraColorName);
        }

        public BurtRenderTargetHandle RegisterRefractionDistortionTexture()
        {
            return RegisterRefractionDistortion(new RenderTargetIdentifier(RefractionDistortionTextureId));
        }

        public BurtRenderTargetHandle RegisterRefractionDistortion(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(RefractionDistortionName, identifier);
        }

        public BurtRenderTargetHandle GetRefractionDistortion()
        {
            return GetRenderTarget(RefractionDistortionName);
        }

        public BurtRenderTargetHandle RegisterRefractionSceneColorMipChainTexture()
        {
            return RegisterRefractionSceneColorMipChain(new RenderTargetIdentifier(RefractionSceneColorMipChainId));
        }

        public BurtRenderTargetHandle RegisterRefractionSceneColorMipChain(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(RefractionSceneColorMipChainName, identifier);
        }

        public BurtRenderTargetHandle GetRefractionSceneColorMipChain()
        {
            return GetRenderTarget(RefractionSceneColorMipChainName);
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

        public BurtRenderTargetHandle RegisterDeferredLightingDepthTexture()
        {
            return RegisterDeferredLightingDepth(new RenderTargetIdentifier(DeferredLightingDepthTextureId));
        }

        public BurtRenderTargetHandle RegisterDeferredLightingDepth(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(DeferredLightingDepthName, identifier);
        }

        public BurtRenderTargetHandle GetDeferredLightingDepth()
        {
            return GetRenderTarget(DeferredLightingDepthName);
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

        public BurtRenderTargetHandle RegisterTemporalAAOutputTexture()
        {
            return RegisterRenderTarget(TemporalAAOutputName, new RenderTargetIdentifier(TemporalAAOutputTextureId));
        }

        public BurtRenderTargetHandle GetTemporalAAOutput()
        {
            return GetRenderTarget(TemporalAAOutputName);
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

        public BurtRenderTargetHandle RegisterGBuffer4Texture()
        {
            return RegisterGBuffer4(new RenderTargetIdentifier(GBuffer4Id));
        }

        public BurtRenderTargetHandle RegisterGBuffer4(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(GBuffer4Name, identifier);
        }

        public BurtRenderTargetHandle GetGBuffer4()
        {
            return GetRenderTarget(GBuffer4Name);
        }

        public BurtRenderTargetHandle RegisterGBuffer5Texture()
        {
            return RegisterGBuffer5(new RenderTargetIdentifier(GBuffer5Id));
        }

        public BurtRenderTargetHandle RegisterGBuffer5(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(GBuffer5Name, identifier);
        }

        public BurtRenderTargetHandle GetGBuffer5()
        {
            return GetRenderTarget(GBuffer5Name);
        }

        public BurtRenderTargetHandle RegisterGBufferObjectIndexTexture()
        {
            return RegisterGBufferObjectIndex(new RenderTargetIdentifier(GBufferObjectIndexId));
        }

        public BurtRenderTargetHandle RegisterGBufferObjectIndex(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(GBufferObjectIndexName, identifier);
        }

        public BurtRenderTargetHandle GetGBufferObjectIndex()
        {
            return GetRenderTarget(GBufferObjectIndexName);
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

        public BurtRenderTargetHandle RegisterScreenSpaceShadowTexture()
        {
            return RegisterScreenSpaceShadow(new RenderTargetIdentifier(ScreenSpaceShadowTextureId));
        }

        public BurtRenderTargetHandle RegisterScreenSpaceShadow(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(ScreenSpaceShadowName, identifier);
        }

        public BurtRenderTargetHandle GetScreenSpaceShadow()
        {
            return GetRenderTarget(ScreenSpaceShadowName);
        }

        public BurtRenderTargetHandle RegisterScreenSpaceGlobalIlluminationRawTexture()
        {
            return RegisterScreenSpaceGlobalIlluminationRaw(new RenderTargetIdentifier(ScreenSpaceGlobalIlluminationRawTextureId));
        }

        public BurtRenderTargetHandle RegisterScreenSpaceGlobalIlluminationRaw(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(ScreenSpaceGlobalIlluminationRawName, identifier);
        }

        public BurtRenderTargetHandle GetScreenSpaceGlobalIlluminationRaw()
        {
            return GetRenderTarget(ScreenSpaceGlobalIlluminationRawName);
        }

        public BurtRenderTargetHandle RegisterScreenSpaceGlobalIlluminationTexture()
        {
            return RegisterScreenSpaceGlobalIllumination(new RenderTargetIdentifier(ScreenSpaceGlobalIlluminationTextureId));
        }

        public BurtRenderTargetHandle RegisterScreenSpaceGlobalIllumination(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(ScreenSpaceGlobalIlluminationName, identifier);
        }

        public BurtRenderTargetHandle GetScreenSpaceGlobalIllumination()
        {
            return GetRenderTarget(ScreenSpaceGlobalIlluminationName);
        }

        public BurtRenderTargetHandle RegisterScreenSpaceGlobalIlluminationUpsampledTexture()
        {
            return RegisterScreenSpaceGlobalIlluminationUpsampled(new RenderTargetIdentifier(ScreenSpaceGlobalIlluminationUpsampledTextureId));
        }

        public BurtRenderTargetHandle RegisterScreenSpaceGlobalIlluminationUpsampled(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(ScreenSpaceGlobalIlluminationUpsampledName, identifier);
        }

        public BurtRenderTargetHandle GetScreenSpaceGlobalIlluminationUpsampled()
        {
            return GetRenderTarget(ScreenSpaceGlobalIlluminationUpsampledName);
        }

        public BurtRenderTargetHandle RegisterBurtGIBackfaceDiffuseIndirectTexture()
        {
            return RegisterBurtGIBackfaceDiffuseIndirect(new RenderTargetIdentifier(BurtGIBackfaceDiffuseIndirectTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIBackfaceDiffuseIndirect(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIBackfaceDiffuseIndirectName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIBackfaceDiffuseIndirect()
        {
            return GetRenderTarget(BurtGIBackfaceDiffuseIndirectName);
        }

        public BurtRenderTargetHandle RegisterBurtGIBackfaceDiffuseIndirectUpsampledTexture()
        {
            return RegisterBurtGIBackfaceDiffuseIndirectUpsampled(new RenderTargetIdentifier(BurtGIBackfaceDiffuseIndirectUpsampledTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIBackfaceDiffuseIndirectUpsampled(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIBackfaceDiffuseIndirectUpsampledName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIBackfaceDiffuseIndirectUpsampled()
        {
            return GetRenderTarget(BurtGIBackfaceDiffuseIndirectUpsampledName);
        }

        public BurtRenderTargetHandle RegisterBurtGIRoughSpecularIndirectTexture()
        {
            return RegisterBurtGIRoughSpecularIndirect(new RenderTargetIdentifier(BurtGIRoughSpecularIndirectTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIRoughSpecularIndirect(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIRoughSpecularIndirectName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIRoughSpecularIndirect()
        {
            return GetRenderTarget(BurtGIRoughSpecularIndirectName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeIntegrateTileClassificationTexture()
        {
            return RegisterBurtGIScreenProbeIntegrateTileClassification(new RenderTargetIdentifier(BurtGIScreenProbeIntegrateTileClassificationTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeIntegrateTileClassification(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeIntegrateTileClassificationName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeIntegrateTileClassification()
        {
            return GetRenderTarget(BurtGIScreenProbeIntegrateTileClassificationName);
        }

        public BurtRenderTargetHandle RegisterBurtGIRoughSpecularIndirectUpsampledTexture()
        {
            return RegisterBurtGIRoughSpecularIndirectUpsampled(new RenderTargetIdentifier(BurtGIRoughSpecularIndirectUpsampledTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIRoughSpecularIndirectUpsampled(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIRoughSpecularIndirectUpsampledName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIRoughSpecularIndirectUpsampled()
        {
            return GetRenderTarget(BurtGIRoughSpecularIndirectUpsampledName);
        }

        public BurtRenderTargetHandle RegisterBurtGITranslucencyVolume0Texture()
        {
            return RegisterBurtGITranslucencyVolume0(new RenderTargetIdentifier(BurtGITranslucencyVolume0TextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGITranslucencyVolume0(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGITranslucencyVolume0Name, identifier);
        }

        public BurtRenderTargetHandle GetBurtGITranslucencyVolume0()
        {
            return GetRenderTarget(BurtGITranslucencyVolume0Name);
        }

        public BurtRenderTargetHandle RegisterBurtGITranslucencyVolume1Texture()
        {
            return RegisterBurtGITranslucencyVolume1(new RenderTargetIdentifier(BurtGITranslucencyVolume1TextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGITranslucencyVolume1(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGITranslucencyVolume1Name, identifier);
        }

        public BurtRenderTargetHandle GetBurtGITranslucencyVolume1()
        {
            return GetRenderTarget(BurtGITranslucencyVolume1Name);
        }

        public BurtRenderTargetHandle RegisterBurtGITranslucencyVolumeFilter0Texture()
        {
            return RegisterBurtGITranslucencyVolumeFilter0(new RenderTargetIdentifier(BurtGITranslucencyVolumeFilter0TextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGITranslucencyVolumeFilter0(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGITranslucencyVolumeFilter0Name, identifier);
        }

        public BurtRenderTargetHandle GetBurtGITranslucencyVolumeFilter0()
        {
            return GetRenderTarget(BurtGITranslucencyVolumeFilter0Name);
        }

        public BurtRenderTargetHandle RegisterBurtGITranslucencyVolumeFilter1Texture()
        {
            return RegisterBurtGITranslucencyVolumeFilter1(new RenderTargetIdentifier(BurtGITranslucencyVolumeFilter1TextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGITranslucencyVolumeFilter1(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGITranslucencyVolumeFilter1Name, identifier);
        }

        public BurtRenderTargetHandle GetBurtGITranslucencyVolumeFilter1()
        {
            return GetRenderTarget(BurtGITranslucencyVolumeFilter1Name);
        }

        public BurtRenderTargetHandle RegisterBurtGITranslucencyVolumeTraceRadianceTexture()
        {
            return RegisterBurtGITranslucencyVolumeTraceRadiance(new RenderTargetIdentifier(BurtGITranslucencyVolumeTraceRadianceTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGITranslucencyVolumeTraceRadiance(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGITranslucencyVolumeTraceRadianceName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGITranslucencyVolumeTraceRadiance()
        {
            return GetRenderTarget(BurtGITranslucencyVolumeTraceRadianceName);
        }

        public BurtRenderTargetHandle RegisterBurtGITranslucencyVolumeTraceFilteredRadianceTexture()
        {
            return RegisterBurtGITranslucencyVolumeTraceFilteredRadiance(new RenderTargetIdentifier(BurtGITranslucencyVolumeTraceFilteredRadianceTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGITranslucencyVolumeTraceFilteredRadiance(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGITranslucencyVolumeTraceFilteredRadianceName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGITranslucencyVolumeTraceFilteredRadiance()
        {
            return GetRenderTarget(BurtGITranslucencyVolumeTraceFilteredRadianceName);
        }

        public BurtRenderTargetHandle RegisterBurtGITranslucencyVolumeTraceHitDistanceTexture()
        {
            return RegisterBurtGITranslucencyVolumeTraceHitDistance(new RenderTargetIdentifier(BurtGITranslucencyVolumeTraceHitDistanceTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGITranslucencyVolumeTraceHitDistance(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGITranslucencyVolumeTraceHitDistanceName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGITranslucencyVolumeTraceHitDistance()
        {
            return GetRenderTarget(BurtGITranslucencyVolumeTraceHitDistanceName);
        }

        public BurtRenderTargetHandle RegisterBurtGISceneVoxelRadianceTexture()
        {
            return RegisterBurtGISceneVoxelRadiance(new RenderTargetIdentifier(BurtGISceneVoxelRadianceTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGISceneVoxelRadiance(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGISceneVoxelRadianceName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGISceneVoxelRadiance()
        {
            return GetRenderTarget(BurtGISceneVoxelRadianceName);
        }

        public BurtRenderTargetHandle RegisterBurtGISceneVoxelGeometryTexture()
        {
            return RegisterBurtGISceneVoxelGeometry(new RenderTargetIdentifier(BurtGISceneVoxelGeometryTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGISceneVoxelGeometry(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGISceneVoxelGeometryName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGISceneVoxelGeometry()
        {
            return GetRenderTarget(BurtGISceneVoxelGeometryName);
        }

        public BurtRenderTargetHandle RegisterBurtGISceneVoxelOccupancyMipTexture()
        {
            return RegisterBurtGISceneVoxelOccupancyMip(new RenderTargetIdentifier(BurtGISceneVoxelOccupancyMipTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGISceneVoxelOccupancyMip(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGISceneVoxelOccupancyMipName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGISceneVoxelOccupancyMip()
        {
            return GetRenderTarget(BurtGISceneVoxelOccupancyMipName);
        }

        public BurtRenderTargetHandle RegisterBurtGISceneVoxelLightingTexture()
        {
            return RegisterBurtGISceneVoxelLighting(new RenderTargetIdentifier(BurtGISceneVoxelLightingTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGISceneVoxelLighting(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGISceneVoxelLightingName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGISceneVoxelLighting()
        {
            return GetRenderTarget(BurtGISceneVoxelLightingName);
        }

        public BurtRenderTargetHandle RegisterBurtGITemporalDiagnosticsTexture()
        {
            return RegisterBurtGITemporalDiagnostics(new RenderTargetIdentifier(BurtGITemporalDiagnosticsTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGITemporalDiagnostics(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGITemporalDiagnosticsName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGITemporalDiagnostics()
        {
            return GetRenderTarget(BurtGITemporalDiagnosticsName);
        }

        public BurtRenderTargetHandle RegisterBurtGIRadianceCacheStatsTexture()
        {
            return RegisterBurtGIRadianceCacheStats(new RenderTargetIdentifier(BurtGIRadianceCacheStatsTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIRadianceCacheStats(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIRadianceCacheStatsName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIRadianceCacheStats()
        {
            return GetRenderTarget(BurtGIRadianceCacheStatsName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeScreenDepthTexture()
        {
            return RegisterBurtGIScreenProbeScreenDepth(new RenderTargetIdentifier(BurtGIScreenProbeScreenDepthTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeScreenDepth(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeScreenDepthName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeScreenDepth()
        {
            return GetRenderTarget(BurtGIScreenProbeScreenDepthName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeWorldNormalTexture()
        {
            return RegisterBurtGIScreenProbeWorldNormal(new RenderTargetIdentifier(BurtGIScreenProbeWorldNormalTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeWorldNormal(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeWorldNormalName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeWorldNormal()
        {
            return GetRenderTarget(BurtGIScreenProbeWorldNormalName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeWorldPositionTexture()
        {
            return RegisterBurtGIScreenProbeWorldPosition(new RenderTargetIdentifier(BurtGIScreenProbeWorldPositionTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeWorldPosition(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeWorldPositionName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeWorldPosition()
        {
            return GetRenderTarget(BurtGIScreenProbeWorldPositionName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeAdaptiveProbeHeaderTexture()
        {
            return RegisterBurtGIScreenProbeAdaptiveProbeHeader(new RenderTargetIdentifier(BurtGIScreenProbeAdaptiveProbeHeaderTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeAdaptiveProbeHeader(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeAdaptiveProbeHeaderName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeAdaptiveProbeHeader()
        {
            return GetRenderTarget(BurtGIScreenProbeAdaptiveProbeHeaderName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeAdaptiveProbeIndicesTexture()
        {
            return RegisterBurtGIScreenProbeAdaptiveProbeIndices(new RenderTargetIdentifier(BurtGIScreenProbeAdaptiveProbeIndicesTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeAdaptiveProbeIndices(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeAdaptiveProbeIndicesName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeAdaptiveProbeIndices()
        {
            return GetRenderTarget(BurtGIScreenProbeAdaptiveProbeIndicesName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeRadianceTexture()
        {
            return RegisterBurtGIScreenProbeRadiance(new RenderTargetIdentifier(BurtGIScreenProbeRadianceTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeRadiance(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeRadianceName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeRadiance()
        {
            return GetRenderTarget(BurtGIScreenProbeRadianceName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeIrradianceTexture()
        {
            return RegisterBurtGIScreenProbeIrradiance(new RenderTargetIdentifier(BurtGIScreenProbeIrradianceTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeIrradiance(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeIrradianceName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeIrradiance()
        {
            return GetRenderTarget(BurtGIScreenProbeIrradianceName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeConfidenceTexture()
        {
            return RegisterBurtGIScreenProbeConfidence(new RenderTargetIdentifier(BurtGIScreenProbeConfidenceTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeConfidence(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeConfidenceName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeConfidence()
        {
            return GetRenderTarget(BurtGIScreenProbeConfidenceName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeHitDistanceTexture()
        {
            return RegisterBurtGIScreenProbeHitDistance(new RenderTargetIdentifier(BurtGIScreenProbeHitDistanceTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeHitDistance(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeHitDistanceName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeHitDistance()
        {
            return GetRenderTarget(BurtGIScreenProbeHitDistanceName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeBentNormalTexture()
        {
            return RegisterBurtGIScreenProbeBentNormal(new RenderTargetIdentifier(BurtGIScreenProbeBentNormalTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeBentNormal(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeBentNormalName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeBentNormal()
        {
            return GetRenderTarget(BurtGIScreenProbeBentNormalName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeTraceRadianceTexture()
        {
            return RegisterBurtGIScreenProbeTraceRadiance(new RenderTargetIdentifier(BurtGIScreenProbeTraceRadianceTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeTraceRadiance(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeTraceRadianceName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeTraceRadiance()
        {
            return GetRenderTarget(BurtGIScreenProbeTraceRadianceName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeTraceHitTexture()
        {
            return RegisterBurtGIScreenProbeTraceHit(new RenderTargetIdentifier(BurtGIScreenProbeTraceHitTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeTraceHit(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeTraceHitName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeTraceHit()
        {
            return GetRenderTarget(BurtGIScreenProbeTraceHitName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeTemporalRadianceTexture()
        {
            return RegisterBurtGIScreenProbeTemporalRadiance(new RenderTargetIdentifier(BurtGIScreenProbeTemporalRadianceTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeTemporalRadiance(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeTemporalRadianceName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeTemporalRadiance()
        {
            return GetRenderTarget(BurtGIScreenProbeTemporalRadianceName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeTemporalIrradianceTexture()
        {
            return RegisterBurtGIScreenProbeTemporalIrradiance(new RenderTargetIdentifier(BurtGIScreenProbeTemporalIrradianceTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeTemporalIrradiance(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeTemporalIrradianceName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeTemporalIrradiance()
        {
            return GetRenderTarget(BurtGIScreenProbeTemporalIrradianceName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeTemporalConfidenceTexture()
        {
            return RegisterBurtGIScreenProbeTemporalConfidence(new RenderTargetIdentifier(BurtGIScreenProbeTemporalConfidenceTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeTemporalConfidence(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeTemporalConfidenceName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeTemporalConfidence()
        {
            return GetRenderTarget(BurtGIScreenProbeTemporalConfidenceName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeFilteredRadianceTexture()
        {
            return RegisterBurtGIScreenProbeFilteredRadiance(new RenderTargetIdentifier(BurtGIScreenProbeFilteredRadianceTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeFilteredRadiance(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeFilteredRadianceName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeFilteredRadiance()
        {
            return GetRenderTarget(BurtGIScreenProbeFilteredRadianceName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeFilteredIrradianceTexture()
        {
            return RegisterBurtGIScreenProbeFilteredIrradiance(new RenderTargetIdentifier(BurtGIScreenProbeFilteredIrradianceTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeFilteredIrradiance(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeFilteredIrradianceName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeFilteredIrradiance()
        {
            return GetRenderTarget(BurtGIScreenProbeFilteredIrradianceName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeFilteredConfidenceTexture()
        {
            return RegisterBurtGIScreenProbeFilteredConfidence(new RenderTargetIdentifier(BurtGIScreenProbeFilteredConfidenceTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeFilteredConfidence(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeFilteredConfidenceName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeFilteredConfidence()
        {
            return GetRenderTarget(BurtGIScreenProbeFilteredConfidenceName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeFixupRadianceTexture()
        {
            return RegisterBurtGIScreenProbeFixupRadiance(new RenderTargetIdentifier(BurtGIScreenProbeFixupRadianceTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeFixupRadiance(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeFixupRadianceName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeFixupRadiance()
        {
            return GetRenderTarget(BurtGIScreenProbeFixupRadianceName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeFixupIrradianceTexture()
        {
            return RegisterBurtGIScreenProbeFixupIrradiance(new RenderTargetIdentifier(BurtGIScreenProbeFixupIrradianceTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeFixupIrradiance(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeFixupIrradianceName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeFixupIrradiance()
        {
            return GetRenderTarget(BurtGIScreenProbeFixupIrradianceName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeFixupConfidenceTexture()
        {
            return RegisterBurtGIScreenProbeFixupConfidence(new RenderTargetIdentifier(BurtGIScreenProbeFixupConfidenceTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeFixupConfidence(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeFixupConfidenceName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeFixupConfidence()
        {
            return GetRenderTarget(BurtGIScreenProbeFixupConfidenceName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeMipRadianceTexture()
        {
            return RegisterBurtGIScreenProbeMipRadiance(new RenderTargetIdentifier(BurtGIScreenProbeMipRadianceTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeMipRadiance(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeMipRadianceName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeMipRadiance()
        {
            return GetRenderTarget(BurtGIScreenProbeMipRadianceName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeMipIrradianceTexture()
        {
            return RegisterBurtGIScreenProbeMipIrradiance(new RenderTargetIdentifier(BurtGIScreenProbeMipIrradianceTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeMipIrradiance(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeMipIrradianceName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeMipIrradiance()
        {
            return GetRenderTarget(BurtGIScreenProbeMipIrradianceName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeMipConfidenceTexture()
        {
            return RegisterBurtGIScreenProbeMipConfidence(new RenderTargetIdentifier(BurtGIScreenProbeMipConfidenceTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeMipConfidence(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeMipConfidenceName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeMipConfidence()
        {
            return GetRenderTarget(BurtGIScreenProbeMipConfidenceName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeMip2RadianceTexture()
        {
            return RegisterBurtGIScreenProbeMip2Radiance(new RenderTargetIdentifier(BurtGIScreenProbeMip2RadianceTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeMip2Radiance(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeMip2RadianceName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeMip2Radiance()
        {
            return GetRenderTarget(BurtGIScreenProbeMip2RadianceName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeMip2IrradianceTexture()
        {
            return RegisterBurtGIScreenProbeMip2Irradiance(new RenderTargetIdentifier(BurtGIScreenProbeMip2IrradianceTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeMip2Irradiance(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeMip2IrradianceName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeMip2Irradiance()
        {
            return GetRenderTarget(BurtGIScreenProbeMip2IrradianceName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeMip2ConfidenceTexture()
        {
            return RegisterBurtGIScreenProbeMip2Confidence(new RenderTargetIdentifier(BurtGIScreenProbeMip2ConfidenceTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeMip2Confidence(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeMip2ConfidenceName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeMip2Confidence()
        {
            return GetRenderTarget(BurtGIScreenProbeMip2ConfidenceName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeMip3RadianceTexture()
        {
            return RegisterBurtGIScreenProbeMip3Radiance(new RenderTargetIdentifier(BurtGIScreenProbeMip3RadianceTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeMip3Radiance(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeMip3RadianceName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeMip3Radiance()
        {
            return GetRenderTarget(BurtGIScreenProbeMip3RadianceName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeMip3IrradianceTexture()
        {
            return RegisterBurtGIScreenProbeMip3Irradiance(new RenderTargetIdentifier(BurtGIScreenProbeMip3IrradianceTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeMip3Irradiance(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeMip3IrradianceName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeMip3Irradiance()
        {
            return GetRenderTarget(BurtGIScreenProbeMip3IrradianceName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeMip3ConfidenceTexture()
        {
            return RegisterBurtGIScreenProbeMip3Confidence(new RenderTargetIdentifier(BurtGIScreenProbeMip3ConfidenceTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeMip3Confidence(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeMip3ConfidenceName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeMip3Confidence()
        {
            return GetRenderTarget(BurtGIScreenProbeMip3ConfidenceName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeRadianceSHAmbientTexture()
        {
            return RegisterBurtGIScreenProbeRadianceSHAmbient(new RenderTargetIdentifier(BurtGIScreenProbeRadianceSHAmbientTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeRadianceSHAmbient(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeRadianceSHAmbientName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeRadianceSHAmbient()
        {
            return GetRenderTarget(BurtGIScreenProbeRadianceSHAmbientName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeRadianceSHDirectionalTexture()
        {
            return RegisterBurtGIScreenProbeRadianceSHDirectional(new RenderTargetIdentifier(BurtGIScreenProbeRadianceSHDirectionalTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeRadianceSHDirectional(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeRadianceSHDirectionalName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeRadianceSHDirectional()
        {
            return GetRenderTarget(BurtGIScreenProbeRadianceSHDirectionalName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeIrradianceOctTexture()
        {
            return RegisterBurtGIScreenProbeIrradianceOct(new RenderTargetIdentifier(BurtGIScreenProbeIrradianceOctTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeIrradianceOct(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeIrradianceOctName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeIrradianceOct()
        {
            return GetRenderTarget(BurtGIScreenProbeIrradianceOctName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeRadianceOctTexture()
        {
            return RegisterBurtGIScreenProbeRadianceOct(new RenderTargetIdentifier(BurtGIScreenProbeRadianceOctTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeRadianceOct(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeRadianceOctName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeRadianceOct()
        {
            return GetRenderTarget(BurtGIScreenProbeRadianceOctName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeImportancePDFTexture()
        {
            return RegisterBurtGIScreenProbeImportancePDF(new RenderTargetIdentifier(BurtGIScreenProbeImportancePDFTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeImportancePDF(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeImportancePDFName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeImportancePDF()
        {
            return GetRenderTarget(BurtGIScreenProbeImportancePDFName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeImportanceLightPDFTexture()
        {
            return RegisterBurtGIScreenProbeImportanceLightPDF(new RenderTargetIdentifier(BurtGIScreenProbeImportanceLightPDFTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeImportanceLightPDF(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeImportanceLightPDFName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeImportanceLightPDF()
        {
            return GetRenderTarget(BurtGIScreenProbeImportanceLightPDFName);
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeImportanceRayInfoTexture()
        {
            return RegisterBurtGIScreenProbeImportanceRayInfo(new RenderTargetIdentifier(BurtGIScreenProbeImportanceRayInfoTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIScreenProbeImportanceRayInfo(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIScreenProbeImportanceRayInfoName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIScreenProbeImportanceRayInfo()
        {
            return GetRenderTarget(BurtGIScreenProbeImportanceRayInfoName);
        }

        public BurtRenderTargetHandle RegisterBurtGIRadianceCacheClipMapIndirectionTexture()
        {
            return RegisterBurtGIRadianceCacheClipMapIndirection(new RenderTargetIdentifier(BurtGIRadianceCacheClipMapIndirectionTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIRadianceCacheClipMapIndirection(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIRadianceCacheClipMapIndirectionName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIRadianceCacheClipMapIndirection()
        {
            return GetRenderTarget(BurtGIRadianceCacheClipMapIndirectionName);
        }

        public BurtRenderTargetHandle RegisterBurtGIRadianceCacheClipMapDepthProbeAtlasTexture()
        {
            return RegisterBurtGIRadianceCacheClipMapDepthProbeAtlas(new RenderTargetIdentifier(BurtGIRadianceCacheClipMapDepthProbeAtlasTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIRadianceCacheClipMapDepthProbeAtlas(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIRadianceCacheClipMapDepthProbeAtlasName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIRadianceCacheClipMapDepthProbeAtlas()
        {
            return GetRenderTarget(BurtGIRadianceCacheClipMapDepthProbeAtlasName);
        }

        public BurtRenderTargetHandle RegisterBurtGIRadianceCacheClipMapRadianceProbeAtlasTexture()
        {
            return RegisterBurtGIRadianceCacheClipMapRadianceProbeAtlas(new RenderTargetIdentifier(BurtGIRadianceCacheClipMapRadianceProbeAtlasTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIRadianceCacheClipMapRadianceProbeAtlas(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIRadianceCacheClipMapRadianceProbeAtlasName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIRadianceCacheClipMapRadianceProbeAtlas()
        {
            return GetRenderTarget(BurtGIRadianceCacheClipMapRadianceProbeAtlasName);
        }

        public BurtRenderTargetHandle RegisterBurtGIRadianceCacheClipMapFinalRadianceAtlasTexture()
        {
            return RegisterBurtGIRadianceCacheClipMapFinalRadianceAtlas(new RenderTargetIdentifier(BurtGIRadianceCacheClipMapFinalRadianceAtlasTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIRadianceCacheClipMapFinalRadianceAtlas(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIRadianceCacheClipMapFinalRadianceAtlasName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIRadianceCacheClipMapFinalRadianceAtlas()
        {
            return GetRenderTarget(BurtGIRadianceCacheClipMapFinalRadianceAtlasName);
        }

        public BurtRenderTargetHandle RegisterBurtGIRadianceCacheClipMapFinalIrradianceAtlasTexture()
        {
            return RegisterBurtGIRadianceCacheClipMapFinalIrradianceAtlas(new RenderTargetIdentifier(BurtGIRadianceCacheClipMapFinalIrradianceAtlasTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIRadianceCacheClipMapFinalIrradianceAtlas(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIRadianceCacheClipMapFinalIrradianceAtlasName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIRadianceCacheClipMapFinalIrradianceAtlas()
        {
            return GetRenderTarget(BurtGIRadianceCacheClipMapFinalIrradianceAtlasName);
        }

        public BurtRenderTargetHandle RegisterBurtGIRadianceCacheClipMapProbeOcclusionAtlasTexture()
        {
            return RegisterBurtGIRadianceCacheClipMapProbeOcclusionAtlas(new RenderTargetIdentifier(BurtGIRadianceCacheClipMapProbeOcclusionAtlasTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIRadianceCacheClipMapProbeOcclusionAtlas(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIRadianceCacheClipMapProbeOcclusionAtlasName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIRadianceCacheClipMapProbeOcclusionAtlas()
        {
            return GetRenderTarget(BurtGIRadianceCacheClipMapProbeOcclusionAtlasName);
        }

        public BurtRenderTargetHandle RegisterBurtGIRadianceCacheClipMapProbeSkyAOAtlasTexture()
        {
            return RegisterBurtGIRadianceCacheClipMapProbeSkyAOAtlas(new RenderTargetIdentifier(BurtGIRadianceCacheClipMapProbeSkyAOAtlasTextureId));
        }

        public BurtRenderTargetHandle RegisterBurtGIRadianceCacheClipMapProbeSkyAOAtlas(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(BurtGIRadianceCacheClipMapProbeSkyAOAtlasName, identifier);
        }

        public BurtRenderTargetHandle GetBurtGIRadianceCacheClipMapProbeSkyAOAtlas()
        {
            return GetRenderTarget(BurtGIRadianceCacheClipMapProbeSkyAOAtlasName);
        }

        public BurtRenderTargetHandle RegisterScreenSpaceSubsurfaceSourceTexture()
        {
            return RegisterScreenSpaceSubsurfaceSource(new RenderTargetIdentifier(ScreenSpaceSubsurfaceSourceTextureId));
        }

        public BurtRenderTargetHandle RegisterScreenSpaceSubsurfaceSource(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(ScreenSpaceSubsurfaceSourceName, identifier);
        }

        public BurtRenderTargetHandle GetScreenSpaceSubsurfaceSource()
        {
            return GetRenderTarget(ScreenSpaceSubsurfaceSourceName);
        }

        public BurtRenderTargetHandle RegisterScreenSpaceSubsurfaceBaseColorTexture()
        {
            return RegisterScreenSpaceSubsurfaceBaseColor(new RenderTargetIdentifier(ScreenSpaceSubsurfaceBaseColorTextureId));
        }

        public BurtRenderTargetHandle RegisterScreenSpaceSubsurfaceBaseColor(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(ScreenSpaceSubsurfaceBaseColorName, identifier);
        }

        public BurtRenderTargetHandle GetScreenSpaceSubsurfaceBaseColor()
        {
            return GetRenderTarget(ScreenSpaceSubsurfaceBaseColorName);
        }

        public BurtRenderTargetHandle RegisterScreenSpaceSubsurfaceEmissionTexture()
        {
            return RegisterScreenSpaceSubsurfaceEmission(new RenderTargetIdentifier(ScreenSpaceSubsurfaceEmissionTextureId));
        }

        public BurtRenderTargetHandle RegisterScreenSpaceSubsurfaceEmission(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(ScreenSpaceSubsurfaceEmissionName, identifier);
        }

        public BurtRenderTargetHandle GetScreenSpaceSubsurfaceEmission()
        {
            return GetRenderTarget(ScreenSpaceSubsurfaceEmissionName);
        }

        public BurtRenderTargetHandle RegisterScreenSpaceSubsurfaceSetupTexture()
        {
            return RegisterScreenSpaceSubsurfaceSetup(new RenderTargetIdentifier(ScreenSpaceSubsurfaceSetupTextureId));
        }

        public BurtRenderTargetHandle RegisterScreenSpaceSubsurfaceSetup(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(ScreenSpaceSubsurfaceSetupName, identifier);
        }

        public BurtRenderTargetHandle GetScreenSpaceSubsurfaceSetup()
        {
            return GetRenderTarget(ScreenSpaceSubsurfaceSetupName);
        }

        public BurtRenderTargetHandle RegisterScreenSpaceSubsurfaceProfileIDAndTypeTexture()
        {
            return RegisterScreenSpaceSubsurfaceProfileIDAndType(new RenderTargetIdentifier(ScreenSpaceSubsurfaceProfileIDAndTypeTextureId));
        }

        public BurtRenderTargetHandle RegisterScreenSpaceSubsurfaceProfileIDAndType(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(ScreenSpaceSubsurfaceProfileIDAndTypeName, identifier);
        }

        public BurtRenderTargetHandle GetScreenSpaceSubsurfaceProfileIDAndType()
        {
            return GetRenderTarget(ScreenSpaceSubsurfaceProfileIDAndTypeName);
        }

        public BurtRenderTargetHandle RegisterScreenSpaceSubsurfaceMaskTexture()
        {
            return RegisterScreenSpaceSubsurfaceMask(new RenderTargetIdentifier(ScreenSpaceSubsurfaceMaskTextureId));
        }

        public BurtRenderTargetHandle RegisterScreenSpaceSubsurfaceMask(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(ScreenSpaceSubsurfaceMaskName, identifier);
        }

        public BurtRenderTargetHandle GetScreenSpaceSubsurfaceMask()
        {
            return GetRenderTarget(ScreenSpaceSubsurfaceMaskName);
        }

        public BurtRenderTargetHandle RegisterScreenSpaceSubsurfaceTempTexture()
        {
            return RegisterScreenSpaceSubsurfaceTemp(new RenderTargetIdentifier(ScreenSpaceSubsurfaceTempTextureId));
        }

        public BurtRenderTargetHandle RegisterScreenSpaceSubsurfaceTemp(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(ScreenSpaceSubsurfaceTempName, identifier);
        }

        public BurtRenderTargetHandle GetScreenSpaceSubsurfaceTemp()
        {
            return GetRenderTarget(ScreenSpaceSubsurfaceTempName);
        }

        public BurtRenderTargetHandle RegisterScreenSpaceSubsurfaceBlurTexture()
        {
            return RegisterScreenSpaceSubsurfaceBlur(new RenderTargetIdentifier(ScreenSpaceSubsurfaceBlurTextureId));
        }

        public BurtRenderTargetHandle RegisterScreenSpaceSubsurfaceBlur(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(ScreenSpaceSubsurfaceBlurName, identifier);
        }

        public BurtRenderTargetHandle GetScreenSpaceSubsurfaceBlur()
        {
            return GetRenderTarget(ScreenSpaceSubsurfaceBlurName);
        }

        public BurtRenderTargetHandle RegisterScreenSpaceSubsurfaceCombineTexture()
        {
            return RegisterScreenSpaceSubsurfaceCombine(new RenderTargetIdentifier(ScreenSpaceSubsurfaceCombineTextureId));
        }

        public BurtRenderTargetHandle RegisterScreenSpaceSubsurfaceCombine(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(ScreenSpaceSubsurfaceCombineName, identifier);
        }

        public BurtRenderTargetHandle GetScreenSpaceSubsurfaceCombine()
        {
            return GetRenderTarget(ScreenSpaceSubsurfaceCombineName);
        }

        public BurtRenderTargetHandle RegisterScreenSpaceSubsurfaceVelocityTexture()
        {
            return RegisterScreenSpaceSubsurfaceVelocity(new RenderTargetIdentifier(ScreenSpaceSubsurfaceVelocityTextureId));
        }

        public BurtRenderTargetHandle RegisterScreenSpaceSubsurfaceVelocity(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(ScreenSpaceSubsurfaceVelocityName, identifier);
        }

        public BurtRenderTargetHandle GetScreenSpaceSubsurfaceVelocity()
        {
            return GetRenderTarget(ScreenSpaceSubsurfaceVelocityName);
        }

        public BurtRenderTargetHandle RegisterFurBlurPropertyTexture()
        {
            return RegisterFurBlurProperty(new RenderTargetIdentifier(FurBlurPropertyTextureId));
        }

        public BurtRenderTargetHandle RegisterFurBlurProperty(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(FurBlurPropertyName, identifier);
        }

        public BurtRenderTargetHandle GetFurBlurProperty()
        {
            return GetRenderTarget(FurBlurPropertyName);
        }

        public BurtRenderTargetHandle RegisterFurBlurPropertyTempTexture()
        {
            return RegisterFurBlurPropertyTemp(new RenderTargetIdentifier(FurBlurPropertyTempTextureId));
        }

        public BurtRenderTargetHandle RegisterFurBlurPropertyTemp(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(FurBlurPropertyTempName, identifier);
        }

        public BurtRenderTargetHandle GetFurBlurPropertyTemp()
        {
            return GetRenderTarget(FurBlurPropertyTempName);
        }

        public BurtRenderTargetHandle RegisterFurBlurColorTexture()
        {
            return RegisterFurBlurColor(new RenderTargetIdentifier(FurBlurColorTextureId));
        }

        public BurtRenderTargetHandle RegisterFurBlurColor(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(FurBlurColorName, identifier);
        }

        public BurtRenderTargetHandle GetFurBlurColor()
        {
            return GetRenderTarget(FurBlurColorName);
        }

        public BurtRenderTargetHandle RegisterFurBlurTemporalTexture()
        {
            return RegisterFurBlurTemporal(new RenderTargetIdentifier(FurBlurTemporalTextureId));
        }

        public BurtRenderTargetHandle RegisterFurBlurTemporal(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(FurBlurTemporalName, identifier);
        }

        public BurtRenderTargetHandle GetFurBlurTemporal()
        {
            return GetRenderTarget(FurBlurTemporalName);
        }

        public BurtRenderTargetHandle RegisterFurBlurVelocityTexture()
        {
            return RegisterFurBlurVelocity(new RenderTargetIdentifier(FurBlurVelocityTextureId));
        }

        public BurtRenderTargetHandle RegisterFurBlurVelocity(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(FurBlurVelocityName, identifier);
        }

        public BurtRenderTargetHandle GetFurBlurVelocity()
        {
            return GetRenderTarget(FurBlurVelocityName);
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

        public BurtRenderTargetHandle RegisterPerObjectShadowAtlasTexture()
        {
            return RegisterPerObjectShadowAtlas(new RenderTargetIdentifier(PerObjectShadowAtlasId));
        }

        public BurtRenderTargetHandle RegisterPerObjectShadowAtlas(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(PerObjectShadowAtlasName, identifier);
        }

        public BurtRenderTargetHandle GetPerObjectShadowAtlas()
        {
            return GetRenderTarget(PerObjectShadowAtlasName);
        }

        private void RecycleAllInternalRenderTextures()
        {
            foreach (var pair in allocatedRenderTextures)
            {
                if (externalRenderTargets.Contains(pair.Key))
                {
                    continue;
                }

                if (renderTargetDescriptors.TryGetValue(pair.Key, out var descriptor) && descriptor.IsValid)
                {
                    ReturnRenderTextureToPool(pair.Value, descriptor);
                }
                else
                {
                    ReleaseRenderTextureObject(pair.Value);
                }
            }
        }

        private void ReleaseInternalRenderTargetIfNeeded(string safeName)
        {
            if (externalRenderTargets.Contains(safeName) ||
                !allocatedRenderTextures.TryGetValue(safeName, out var texture))
            {
                return;
            }

            if (renderTargetDescriptors.TryGetValue(safeName, out var descriptor) && descriptor.IsValid)
            {
                ReturnRenderTextureToPool(texture, descriptor);
            }
            else
            {
                ReleaseRenderTextureObject(texture);
            }

            allocatedRenderTextures.Remove(safeName);
        }

        private RenderTexture TryTakePooledRenderTexture(BurtRenderTextureDescriptor descriptor)
        {
            var key = new BurtRenderTexturePoolKey(descriptor);
            if (!availableRenderTexturePool.TryGetValue(key, out var pooledTextures) || pooledTextures.Count == 0)
            {
                return null;
            }

            return pooledTextures.Pop();
        }

        private void ReturnRenderTextureToPool(RenderTexture texture, BurtRenderTextureDescriptor descriptor)
        {
            if (texture == null)
            {
                return;
            }

            var key = new BurtRenderTexturePoolKey(descriptor);
            if (!availableRenderTexturePool.TryGetValue(key, out var pooledTextures))
            {
                pooledTextures = new Stack<RenderTexture>();
                availableRenderTexturePool.Add(key, pooledTextures);
            }

            pooledTextures.Push(texture);
        }

        private static void ReleaseRenderTextureObject(RenderTexture texture)
        {
            if (texture == null)
            {
                return;
            }

            texture.Release();
            CoreUtils.Destroy(texture);
        }

        private string CreatePhysicalRenderTextureName(RenderTextureDescriptor descriptor)
        {
            var dimensions = descriptor.width + "x" + descriptor.height;
            if (descriptor.volumeDepth > 1)
            {
                dimensions += "x" + descriptor.volumeDepth;
            }

            var usage = descriptor.enableRandomWrite ? " UAV" : string.Empty;
            return "BRP.Transient/RT#" + nextPhysicalRenderTextureIndex++ +
                " [" + dimensions + " " + descriptor.graphicsFormat + usage + "]";
        }

        private void RecycleAllInternalBuffers() // Returns surviving graph-owned buffers to the descriptor pool before the registry is reset.
        {
            foreach (var pair in buffers)
            {
                if (externalBuffers.Contains(pair.Key))
                {
                    continue;
                }

                if (!pair.Value.HasBuffer)
                {
                    continue;
                }

                if (bufferDescriptors.TryGetValue(pair.Key, out var descriptor) && descriptor.IsValid)
                {
                    ReturnBufferToPool(pair.Value.Buffer, descriptor);
                }
                else
                {
                    QueueBufferReleaseObject(pair.Value.Buffer);
                }
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

        private GraphicsBuffer TryTakePooledBuffer(BurtRenderBufferDescriptor descriptor)
        {
            var key = new BurtRenderBufferPoolKey(descriptor);
            if (!availableBufferPool.TryGetValue(key, out var pooledBuffers) || pooledBuffers.Count == 0)
            {
                return null;
            }

            return pooledBuffers.Pop();
        }

        private void ReturnBufferToPool(GraphicsBuffer buffer, BurtRenderBufferDescriptor descriptor)
        {
            if (buffer == null)
            {
                return;
            }

            var key = new BurtRenderBufferPoolKey(descriptor);
            if (!availableBufferPool.TryGetValue(key, out var pooledBuffers))
            {
                pooledBuffers = new Stack<GraphicsBuffer>();
                availableBufferPool.Add(key, pooledBuffers);
            }

            pooledBuffers.Push(buffer);
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

        private readonly struct BurtRenderTexturePoolKey : IEquatable<BurtRenderTexturePoolKey>
        {
            public BurtRenderTexturePoolKey(BurtRenderTextureDescriptor allocationDescriptor)
            {
                var descriptor = allocationDescriptor.Descriptor;
                Width = descriptor.width;
                Height = descriptor.height;
                VolumeDepth = descriptor.volumeDepth;
                MsaaSamples = descriptor.msaaSamples;
                MipCount = descriptor.mipCount;
                DepthBufferBits = descriptor.depthBufferBits;
                GraphicsFormat = descriptor.graphicsFormat;
                DepthStencilFormat = descriptor.depthStencilFormat;
                Dimension = descriptor.dimension;
                EnableRandomWrite = descriptor.enableRandomWrite;
                UseMipMap = descriptor.useMipMap;
                AutoGenerateMips = descriptor.autoGenerateMips;
                Memoryless = descriptor.memoryless;
                VrUsage = descriptor.vrUsage;
                ShadowSamplingMode = descriptor.shadowSamplingMode;
                BindMs = descriptor.bindMS;
                UseDynamicScale = descriptor.useDynamicScale;
                FilterMode = allocationDescriptor.FilterMode;
            }

            private int Width { get; }
            private int Height { get; }
            private int VolumeDepth { get; }
            private int MsaaSamples { get; }
            private int MipCount { get; }
            private int DepthBufferBits { get; }
            private UnityEngine.Experimental.Rendering.GraphicsFormat GraphicsFormat { get; }
            private UnityEngine.Experimental.Rendering.GraphicsFormat DepthStencilFormat { get; }
            private UnityEngine.Rendering.TextureDimension Dimension { get; }
            private bool EnableRandomWrite { get; }
            private bool UseMipMap { get; }
            private bool AutoGenerateMips { get; }
            private RenderTextureMemoryless Memoryless { get; }
            private VRTextureUsage VrUsage { get; }
            private ShadowSamplingMode ShadowSamplingMode { get; }
            private bool BindMs { get; }
            private bool UseDynamicScale { get; }
            private FilterMode FilterMode { get; }

            public bool Equals(BurtRenderTexturePoolKey other)
            {
                return Width == other.Width &&
                    Height == other.Height &&
                    VolumeDepth == other.VolumeDepth &&
                    MsaaSamples == other.MsaaSamples &&
                    MipCount == other.MipCount &&
                    DepthBufferBits == other.DepthBufferBits &&
                    GraphicsFormat == other.GraphicsFormat &&
                    DepthStencilFormat == other.DepthStencilFormat &&
                    Dimension == other.Dimension &&
                    EnableRandomWrite == other.EnableRandomWrite &&
                    UseMipMap == other.UseMipMap &&
                    AutoGenerateMips == other.AutoGenerateMips &&
                    Memoryless == other.Memoryless &&
                    VrUsage == other.VrUsage &&
                    ShadowSamplingMode == other.ShadowSamplingMode &&
                    BindMs == other.BindMs &&
                    UseDynamicScale == other.UseDynamicScale &&
                    FilterMode == other.FilterMode;
            }

            public override bool Equals(object obj)
            {
                return obj is BurtRenderTexturePoolKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = Width;
                    hashCode = (hashCode * 397) ^ Height;
                    hashCode = (hashCode * 397) ^ VolumeDepth;
                    hashCode = (hashCode * 397) ^ MsaaSamples;
                    hashCode = (hashCode * 397) ^ MipCount;
                    hashCode = (hashCode * 397) ^ DepthBufferBits;
                    hashCode = (hashCode * 397) ^ (int)GraphicsFormat;
                    hashCode = (hashCode * 397) ^ (int)DepthStencilFormat;
                    hashCode = (hashCode * 397) ^ (int)Dimension;
                    hashCode = (hashCode * 397) ^ EnableRandomWrite.GetHashCode();
                    hashCode = (hashCode * 397) ^ UseMipMap.GetHashCode();
                    hashCode = (hashCode * 397) ^ AutoGenerateMips.GetHashCode();
                    hashCode = (hashCode * 397) ^ (int)Memoryless;
                    hashCode = (hashCode * 397) ^ (int)VrUsage;
                    hashCode = (hashCode * 397) ^ (int)ShadowSamplingMode;
                    hashCode = (hashCode * 397) ^ BindMs.GetHashCode();
                    hashCode = (hashCode * 397) ^ UseDynamicScale.GetHashCode();
                    hashCode = (hashCode * 397) ^ (int)FilterMode;
                    return hashCode;
                }
            }
        }

        private readonly struct BurtRenderBufferPoolKey : IEquatable<BurtRenderBufferPoolKey>
        {
            public BurtRenderBufferPoolKey(BurtRenderBufferDescriptor descriptor)
            {
                Count = descriptor.Count;
                Stride = descriptor.Stride;
                Target = descriptor.Target;
            }

            private int Count { get; }
            private int Stride { get; }
            private GraphicsBuffer.Target Target { get; }

            public bool Equals(BurtRenderBufferPoolKey other)
            {
                return Count == other.Count && Stride == other.Stride && Target == other.Target;
            }

            public override bool Equals(object obj)
            {
                return obj is BurtRenderBufferPoolKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = Count;
                    hashCode = (hashCode * 397) ^ Stride;
                    hashCode = (hashCode * 397) ^ (int)Target;
                    return hashCode;
                }
            }
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
