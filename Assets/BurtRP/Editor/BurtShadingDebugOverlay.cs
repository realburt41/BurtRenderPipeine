using System.Collections.Generic; // 使用 List 保存当前 SceneView 中已经创建的分类 Dropdown，切换 Debug 时统一刷新显示状态。
using Burt.RenderPipeline; // 读取 BurtShadingDebugSettings，并把 Overlay 选择同步给运行时 Shader 全局参数。
using UnityEditor; // 使用 EditorWindow、MenuItem、SerializedObject、EditorGUILayout 等编辑器 API。
using UnityEditor.Overlays; // 使用 SceneView Overlay API，参考 XRender Editor/XShaderDebug/XShaderDebugUI.cs。
using UnityEditor.Toolbars; // 使用 EditorToolbarDropdownToggle，和 XRender 的多工具栏 Dropdown 组织方式一致。
using UnityEngine; // 使用 Vector2、Rect、Mathf、ObjectNames 等 Unity 类型。
using UnityEngine.Rendering; // 使用 GraphicsSettings / QualitySettings 获取当前 BurtRenderPipelineAsset。
using UnityEngine.UIElements; // 使用 AttachToPanelEvent / DetachFromPanelEvent 管理 Dropdown 生命周期。

namespace Burt.RenderPipeline.Editor // 编辑器扩展放在 BurtRP Editor 命名空间，避免污染运行时命名空间。
{
    internal static class BurtShadingDebugDisplayNames // 集中管理 Debug 显示名，避免 UI 直接暴露 enum 工程名。
    {
        public static string GetDisplayName(BurtShadingDebugMode mode) // 返回面向美术/调试使用的菜单名，参考 XRender DebugDefine.hlsl 的 Display 名。
        {
            switch (mode) // 按 enum 明确映射，新增 Debug 时可以在这里补友好名字。
            {
                case BurtShadingDebugMode.None:
                    return "None"; // 关闭 Shading Debug，返回正常渲染。
                case BurtShadingDebugMode.Albedo:
                    return "Base Color"; // 对齐 XRender Material Debug 的 Base Color 语义。
                case BurtShadingDebugMode.NormalWS:
                    return "Normal World Space"; // 显示法线贴图影响后的世界空间法线。
                case BurtShadingDebugMode.Smoothness:
                    return "Smoothness"; // 显示材质面板语义下的光滑度。
                case BurtShadingDebugMode.Metallic:
                    return "Metallic"; // 显示最终金属度。
                case BurtShadingDebugMode.Occlusion:
                    return "Ambient Occlusion"; // 显示材质 AO 输入。
                case BurtShadingDebugMode.Reflectance:
                    return "Reflectance"; // XRender 风格介质 reflectance 输入，不暴露 F0 面板参数。
                case BurtShadingDebugMode.Roughness:
                    return "Roughness"; // 显示由 smoothness 还原的感知粗糙度。
                case BurtShadingDebugMode.SpecularAARoughness:
                    return "Specular AA Roughness"; // 显示高光 AA 后真正进入 GGX 的 roughness。
                case BurtShadingDebugMode.SpecularEnergyCompensation:
                    return "Specular Energy Compensation"; // 显示直接高光多次散射补能。
                case BurtShadingDebugMode.SpecularOcclusion:
                    return "Specular Occlusion"; // 显示间接高光遮蔽。
                case BurtShadingDebugMode.EnergyPreservation:
                    return "Energy Preservation"; // 显示 XRender diffuse 底层保能比例。
                case BurtShadingDebugMode.IndirectSpecularEnergyCompensation:
                    return "Indirect Specular Energy Compensation"; // 显示环境高光补能。
                case BurtShadingDebugMode.DiffuseColor:
                    return "Diffuse Color"; // 显示 metallic 扣除后的漫反射颜色。
                case BurtShadingDebugMode.Height:
                    return "Height";
                case BurtShadingDebugMode.DirectBRDFD:
                    return "Direct BRDF D (GGX)"; // 显示 GGX NDF D 项。
                case BurtShadingDebugMode.DirectBRDFVisibility:
                    return "Direct BRDF Visibility"; // 显示 Smith Joint Visibility 项。
                case BurtShadingDebugMode.DirectBRDFFresnel:
                    return "Direct BRDF Fresnel"; // 显示 Schlick Fresnel 项。
                case BurtShadingDebugMode.DirectDiffuseLobe:
                    return "Direct Diffuse Lobe"; // 显示 diffuse lobe。
                case BurtShadingDebugMode.DirectDiffuseBRDF:
                    return "Direct Diffuse BRDF"; // 显示未乘 NdotL / LightColor 的 diffuse BRDF。
                case BurtShadingDebugMode.DirectSpecularBRDF:
                    return "Direct Specular BRDF"; // 显示未乘 NdotL / LightColor 的 specular BRDF。
                case BurtShadingDebugMode.SpecularAANormalVariance:
                    return "Specular AA Normal Variance"; // 显示 Normal Filtering 估算的法线方差。
                case BurtShadingDebugMode.SpecularAARoughnessDelta:
                    return "Specular AA Roughness Delta"; // 显示 Specular AA 增加的 roughness。
                case BurtShadingDebugMode.IndirectSpecularDFG:
                    return "Indirect Specular DFG"; // 显示 PreIntegratedFG 采样得到的 DFG.xy。
                case BurtShadingDebugMode.IndirectSpecularEnvBRDF:
                    return "Indirect Specular Env BRDF"; // 显示 DFG 作用到 F0/F90 后的环境 BRDF。
                case BurtShadingDebugMode.SubsurfaceProfileId:
                    return "Subsurface Profile ID";
                case BurtShadingDebugMode.SubsurfaceTransmission:
                    return "Subsurface Transmission";
                case BurtShadingDebugMode.SubsurfaceDirectTransmission:
                    return "Subsurface Direct Transmission";
                case BurtShadingDebugMode.SubsurfaceTransmissionBRDF:
                    return "Subsurface Transmission BRDF";
                case BurtShadingDebugMode.SubsurfaceTransmissionShadow:
                    return "Subsurface Transmission Shadow";
                case BurtShadingDebugMode.SubsurfaceTransmissionPhase:
                    return "Subsurface Transmission Phase";
                case BurtShadingDebugMode.SubsurfaceTransmissionThickness:
                    return "Subsurface Transmission Thickness";
                case BurtShadingDebugMode.SubsurfaceKernelWeight:
                    return "Subsurface Kernel Weight";
                case BurtShadingDebugMode.SubsurfaceIndirect:
                    return "Subsurface Indirect";
                case BurtShadingDebugMode.GBufferBaseColor:
                    return "GBuffer Base Color"; // 显示 GBuffer0.rgb 解码后的 BaseColor。
                case BurtShadingDebugMode.GBufferNormalWS:
                    return "GBuffer Direction WS"; // 显示 GBuffer1.rg 解码后的向量槽。
                case BurtShadingDebugMode.GBufferMetallic:
                    return "GBuffer Material Channel"; // 显示 GBuffer1.b 解码后的材质通道。
                case BurtShadingDebugMode.GBufferSmoothness:
                    return "GBuffer Smoothness"; // 显示 GBuffer1.a 解码后的 Smoothness。
                case BurtShadingDebugMode.GBufferOcclusion:
                    return "GBuffer Occlusion"; // 显示 GBuffer0.a 解码后的 AO。
                case BurtShadingDebugMode.GBufferReflectance:
                    return "GBuffer Reflectance"; // 显示 GBuffer2.a 解码后的 Reflectance。
                case BurtShadingDebugMode.GBufferRoughness:
                    return "GBuffer Roughness"; // 显示 GBuffer -> PBRMaterialData 后的 roughness。
                case BurtShadingDebugMode.GBufferDiffuseColor:
                    return "GBuffer Diffuse Color"; // 显示 GBuffer -> PBRMaterialData 后的 DiffuseColor。
                case BurtShadingDebugMode.GBufferHairStrandDirection:
                    return "GBuffer Hair Strand"; // 显示 Hair 复用 GBuffer1.rg 存储的 strand direction。
                case BurtShadingDebugMode.GBufferHairScatter:
                    return "GBuffer Hair Scatter"; // 显示 Hair 复用 GBuffer1.b 存储的 scatter。
                case BurtShadingDebugMode.GBufferHairShift:
                    return "GBuffer Hair Shift"; // 显示 Hair 复用 GBuffer1.b 存储的 longitudinal shift scale。
                case BurtShadingDebugMode.GBufferClearCoatMask:
                    return "GBuffer Clear Coat Mask";
                case BurtShadingDebugMode.GBufferClearCoatNormalWS:
                    return "GBuffer Clear Coat Normal";
                case BurtShadingDebugMode.GBufferClearCoatRoughness:
                    return "GBuffer Clear Coat Roughness";
                case BurtShadingDebugMode.GBufferSubsurfaceStrength:
                    return "GBuffer Subsurface";
                case BurtShadingDebugMode.GBufferSubsurfaceThickness:
                    return "GBuffer Subsurface Thickness";
                case BurtShadingDebugMode.GBufferSubsurfaceProfileIndex:
                    return "GBuffer Subsurface Profile";
                case BurtShadingDebugMode.GBufferFoliageTransmissionColor:
                    return "GBuffer Foliage Transmission";
                case BurtShadingDebugMode.GBufferFoliageTransmissionWeight:
                    return "GBuffer Foliage Weight";
                case BurtShadingDebugMode.GBufferFoliageThickness:
                    return "GBuffer Foliage Thickness";
                case BurtShadingDebugMode.GBufferFoliageTransmissionNdotL:
                    return "GBuffer Foliage NdotL";
                case BurtShadingDebugMode.GBufferFoliageSpecularScale:
                    return "GBuffer Foliage Specular";
                case BurtShadingDebugMode.GBufferFoliageScreenSpaceShadowIntensity:
                    return "GBuffer Foliage SS Shadow";
                case BurtShadingDebugMode.GBufferAnisotropy:
                    return "GBuffer Anisotropy";
                case BurtShadingDebugMode.GBufferTangentWS:
                    return "GBuffer Tangent WS";
                case BurtShadingDebugMode.DetailLighting:
                    return "Detail Lighting"; // 对齐 XRender DEBUGID_LIGHTING_DETAIL_LIGHTING。
                case BurtShadingDebugMode.IndirectLighting:
                    return "Indirect Lighting Total"; // 显示 SH diffuse + reflection probe specular。
                case BurtShadingDebugMode.DirectDiffuse:
                    return "Direct Diffuse"; // 显示直接漫反射最终贡献。
                case BurtShadingDebugMode.DirectSpecular:
                    return "Direct Specular"; // 显示直接高光最终贡献。
                case BurtShadingDebugMode.AdditionalLighting:
                    return "Additional Lighting"; // 只显示追加光直接光总和。
                case BurtShadingDebugMode.AdditionalLightingUnshadowed:
                    return "Additional Lighting Unshadowed"; // 显示追加光不乘 additional shadow attenuation 时的直接贡献。
                case BurtShadingDebugMode.AdditionalDiffuse:
                    return "Additional Diffuse"; // 只显示追加光漫反射。
                case BurtShadingDebugMode.AdditionalSpecular:
                    return "Additional Specular"; // 只显示追加光高光。
                case BurtShadingDebugMode.IndirectDiffuse:
                    return "Indirect Diffuse"; // 显示 SH / Light Probe 漫反射。
                case BurtShadingDebugMode.IndirectSpecular:
                    return "Indirect Specular"; // 显示 Reflection Probe / Sky 高光。
                case BurtShadingDebugMode.ShadowAttenuation:
                    return "Shadow Attenuation"; // 显示主光阴影衰减，白色表示不在阴影中。
                case BurtShadingDebugMode.AdditionalShadowAttenuation:
                    return "Additional Shadow Attenuation"; // 显示追加光阴影衰减，白色表示不在追加光阴影中。
                case BurtShadingDebugMode.AdditionalShadowFace:
                    return "Additional Shadow Face";
                case BurtShadingDebugMode.AdditionalShadowUV:
                    return "Additional Shadow UV";
                case BurtShadingDebugMode.AdditionalShadowDepth:
                    return "Additional Shadow Depth";
                case BurtShadingDebugMode.AdditionalShadowDepthDelta:
                    return "Additional Shadow Depth Delta";
                case BurtShadingDebugMode.ShadowCascadeIndex:
                    return "Shadow Cascade Index"; // 用颜色区分当前像素命中的 CSM cascade。
                case BurtShadingDebugMode.ShadowCascadeBlend:
                    return "Shadow Cascade Blend"; // 显示 cascade 边界混合权重。
                case BurtShadingDebugMode.ShadowDistanceFade:
                    return "Shadow Distance Fade"; // 显示最后一级 cascade 的远距离阴影淡出。
                case BurtShadingDebugMode.ShadowPCSSRadius:
                    return "Shadow PCSS Radius (Hard Only)"; // 显示 PCSS 估算出的半影半径，仅在 Hard Shadows + PCSS 下有意义。
                case BurtShadingDebugMode.ShadowReceiverDepthDelta:
                    return "Shadow Receiver Depth Delta"; // 显示 receiver depth 与 shadow map 深度差。
                case BurtShadingDebugMode.ShadowPCSSBlockerFraction:
                    return "Shadow PCSS Blockers (Hard Only)"; // 显示 blocker search 里命中的 blocker 占比，仅在 Hard Shadows + PCSS 下有意义。
                case BurtShadingDebugMode.AmbientOcclusion:
                    return "Ambient Occlusion (Lighting)"; // 显示真正参与间接光遮蔽的 AO。
                case BurtShadingDebugMode.Emission:
                    return "Emission"; // 显示自发光贡献。
                case BurtShadingDebugMode.FinalLighting:
                    return "Final Lighting"; // 显示写入 CameraColor 前的最终材质颜色。
                case BurtShadingDebugMode.HairPrimaryLobe:
                    return "Hair Primary Lobe"; // 显示 Hair R/Primary 高光 lobe。
                case BurtShadingDebugMode.HairSecondaryLobe:
                    return "Hair Secondary Lobe"; // 显示 Hair TT/Secondary 彩色高光 lobe。
                case BurtShadingDebugMode.HairTransmissionLobe:
                    return "Hair Transmission Lobe"; // 显示 Hair 近似背光透射 lobe。
                case BurtShadingDebugMode.HairScatter:
                    return "Hair Scatter Lighting"; // 显示参与 Hair lighting 的 scatter。
                case BurtShadingDebugMode.HairAdditionalLighting:
                    return "Hair Additional Lighting"; // 只显示 Hair 像素的追加光直接光。
                case BurtShadingDebugMode.TileLightCount:
                    return "Tile Light Count";
                case BurtShadingDebugMode.TileLightOccupancy:
                    return "Tile Light Occupancy";
                case BurtShadingDebugMode.ClusterLightCount:
                    return "Cluster Light Count";
                case BurtShadingDebugMode.ClusterLightOccupancy:
                    return "Cluster Light Occupancy";
                case BurtShadingDebugMode.CameraDepth:
                    return "Camera Depth"; // 已有全屏深度调试。
                case BurtShadingDebugMode.MainLightShadow:
                    return "Main Light Shadow"; // 已有主光阴影图调试。
                case BurtShadingDebugMode.PerObjectShadowAtlas:
                    return "Per Object Shadow Atlas";
                case BurtShadingDebugMode.PerObjectShadowObjectIndex:
                    return "Per Object Shadow Object Index";
                case BurtShadingDebugMode.PerObjectShadowSlice:
                    return "Per Object Shadow Slice";
                case BurtShadingDebugMode.PerObjectShadowUV:
                    return "Per Object Shadow UV";
                case BurtShadingDebugMode.PerObjectShadowDepth:
                    return "Per Object Shadow Depth";
                case BurtShadingDebugMode.PerObjectShadowCompare:
                    return "Per Object Shadow Compare";
                case BurtShadingDebugMode.PerObjectShadowTransmissionDepth:
                    return "Per Object Transmission Depth";
                case BurtShadingDebugMode.PerObjectShadowTransmissionThickness:
                    return "Per Object Transmission Thickness";
                case BurtShadingDebugMode.ScreenSpaceAmbientOcclusionRaw:
                    return "SSAO Raw";
                case BurtShadingDebugMode.ScreenSpaceAmbientOcclusionFinal:
                    return "SSAO Final";
                case BurtShadingDebugMode.ScreenSpaceAmbientOcclusionOverlay:
                    return "SSAO Overlay";
                case BurtShadingDebugMode.ScreenSpaceAmbientOcclusionHistory:
                    return "SSAO History";
                case BurtShadingDebugMode.ScreenSpaceAmbientOcclusionDifference:
                    return "SSAO Temporal Difference";
                case BurtShadingDebugMode.ScreenSpaceAmbientOcclusionDepthValidity:
                    return "SSAO Depth Validity";
                case BurtShadingDebugMode.ScreenSpaceAmbientOcclusionSurfaceStability:
                    return "SSAO Surface Stability";
                case BurtShadingDebugMode.ScreenSpaceAmbientOcclusionDiagnosticCompare:
                    return "SSAO Diagnostic Compare";
                case BurtShadingDebugMode.ScreenSpaceShadow:
                    return "SS Shadow";
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationRaw:
                    return "BurtGI Raw";
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationFinal:
                    return "BurtGI Final";
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationHitRatio:
                    return "BurtGI Hit Ratio";
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationOverlay:
                    return "BurtGI Overlay";
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationComposite:
                    return "BurtGI Composite";
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationTemporalConfidence:
                    return "BurtGI Temporal Confidence";
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationTemporalRejection:
                    return "BurtGI Temporal Rejection";
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationHistory:
                    return "BurtGI History";
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationDifference:
                    return "BurtGI Difference";
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationLeakGuard:
                    return "BurtGI Leak Guard";
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationDiagnosticCompare:
                    return "BurtGI Diagnostic Compare";
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationConfidence:
                    return "BurtGI Confidence";
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceSetup:
                    return "SSS Setup";
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceMask:
                    return "SSS Mask";
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceBlur:
                    return "SSS Blur";
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceCombine:
                    return "SSS Combine";
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceProfileTintRaw:
                    return "SSS Profile Tint Raw";
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceProfileTintedFinal:
                    return "SSS Profile Tinted Final";
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceBlurAlpha:
                    return "SSS Blur Alpha/Depth";
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceBlurRadius:
                    return "SSS Blur Radius";
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceBlurDelta:
                    return "SSS Blur Delta";
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceSeparableValidity:
                    return "SSS 4S Validity";
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceSeparableIO:
                    return "SSS 4S IO";
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceSeparableChain:
                    return "SSS 4S Chain";
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceAlgorithm:
                    return "SSS Algorithm";
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceHistory:
                    return "SSS History";
                case BurtShadingDebugMode.FurBlurDirection:
                    return "Fur Blur Direction";
                case BurtShadingDebugMode.FurBlurPropertyDepth:
                    return "Fur Blur Depth";
                case BurtShadingDebugMode.FurBlurCurrent:
                    return "Fur Blur Current";
                case BurtShadingDebugMode.FurBlurTemporal:
                    return "Fur Blur Temporal";
                case BurtShadingDebugMode.FurBlurHistory:
                    return "Fur Blur History";
                case BurtShadingDebugMode.FurBlurDiagnostic:
                    return "Fur Blur Diagnostic";
                case BurtShadingDebugMode.BloomPrefilter:
                    return "Bloom Prefilter";
                case BurtShadingDebugMode.BloomFinalBloom:
                    return "Bloom Final";
                case BurtShadingDebugMode.BloomMip1:
                    return "Bloom Mip 1";
                case BurtShadingDebugMode.BloomMip2:
                    return "Bloom Mip 2";
                case BurtShadingDebugMode.BloomMip3:
                    return "Bloom Mip 3";
                case BurtShadingDebugMode.BloomMip4:
                    return "Bloom Mip 4";
                case BurtShadingDebugMode.BloomMip5:
                    return "Bloom Mip 5";
                case BurtShadingDebugMode.BloomAlpha:
                    return "Bloom Alpha";
                case BurtShadingDebugMode.BloomThresholdMask:
                    return "Bloom Threshold Mask";
                case BurtShadingDebugMode.Atmosphere:
                    return "Atmosphere Summary";
                case BurtShadingDebugMode.AtmosphereRayleigh:
                    return "Atmosphere Rayleigh";
                case BurtShadingDebugMode.AtmosphereMie:
                    return "Atmosphere Mie";
                case BurtShadingDebugMode.AtmosphereTransmittance:
                    return "Atmosphere Transmittance";
                case BurtShadingDebugMode.AtmosphereAerialTransmittance:
                    return "Atmosphere Aerial Transmittance";
                case BurtShadingDebugMode.AtmosphereAerialInscatter:
                    return "Atmosphere Aerial Inscatter";
                case BurtShadingDebugMode.AtmosphereAerialFogAmount:
                    return "Atmosphere Aerial Fog Amount";
                case BurtShadingDebugMode.AtmosphereAerialHeightFade:
                    return "Atmosphere Aerial Height Fade";
                case BurtShadingDebugMode.AtmosphereAerialSummary:
                    return "Atmosphere Aerial Summary";
                case BurtShadingDebugMode.AtmosphereSunDisk:
                    return "Atmosphere Sun Disk";
                case BurtShadingDebugMode.AtmosphereSunHalo:
                    return "Atmosphere Sun Halo";
                case BurtShadingDebugMode.AtmosphereHorizon:
                    return "Atmosphere Horizon";
                case BurtShadingDebugMode.AtmosphereGroundBlend:
                    return "Atmosphere Ground Blend";
                case BurtShadingDebugMode.AtmosphereViewDirection:
                    return "Atmosphere View Direction";
                case BurtShadingDebugMode.FogAmount:
                    return "Fog Amount";
                case BurtShadingDebugMode.FogTransmittance:
                    return "Fog Transmittance";
                case BurtShadingDebugMode.FogHeight:
                    return "Fog Height";
                case BurtShadingDebugMode.FogDistance:
                    return "Fog Distance";
                case BurtShadingDebugMode.VolumetricFogScattering:
                    return "Volumetric Fog Scattering";
                case BurtShadingDebugMode.VolumetricFogTransmittance:
                    return "Volumetric Fog Transmittance";
                case BurtShadingDebugMode.VolumetricFogDensity:
                    return "Volumetric Fog Density";
                case BurtShadingDebugMode.VolumetricFogDistance:
                    return "Volumetric Fog Distance";
                case BurtShadingDebugMode.VolumetricFogStepCount:
                    return "Volumetric Fog Step Count";
                case BurtShadingDebugMode.AutoExposureLuminance:
                    return "Auto Exposure Luminance";
                case BurtShadingDebugMode.AutoExposureMeteringWeight:
                    return "Auto Exposure Metering Weight";
                case BurtShadingDebugMode.AutoExposureHistogramRange:
                    return "Auto Exposure Histogram Range";
                case BurtShadingDebugMode.ScreenSpaceReflectionRawHitMask:
                    return "SSR Raw Hit Mask";
                case BurtShadingDebugMode.ScreenSpaceReflectionHitMask:
                    return "SSR Hit Mask"; // 显示最终参与 composite 的 SSR 遮罩。
                case BurtShadingDebugMode.ScreenSpaceReflectionHitUV:
                    return "SSR Hit UV"; // 显示 SSR 命中点屏幕 UV。
                case BurtShadingDebugMode.ScreenSpaceReflectionStepCount:
                    return "SSR Step Count"; // 显示 SSR raymarch 步数。
                case BurtShadingDebugMode.ScreenSpaceReflectionColor:
                    return "SSR Reflection Color"; // 显示 SSR 命中后采样到的反射颜色。
                case BurtShadingDebugMode.ScreenSpaceReflectionConfidence:
                    return "SSR Confidence";
                case BurtShadingDebugMode.ScreenSpaceReflectionDepthDelta:
                    return "SSR Depth Delta";
                case BurtShadingDebugMode.ScreenSpaceReflectionWorldError:
                    return "SSR World Error";
                case BurtShadingDebugMode.ScreenSpaceReflectionDenoisedColor:
                    return "SSR Denoised Color";
                case BurtShadingDebugMode.ScreenSpaceReflectionTemporalColor:
                    return "SSR Temporal Color";
                case BurtShadingDebugMode.ScreenSpaceReflectionResolveAlpha:
                    return "SSR Resolve Alpha";
                case BurtShadingDebugMode.ScreenSpaceReflectionVisibilityAlpha:
                    return "SSR Visibility Alpha";
                case BurtShadingDebugMode.ScreenSpaceReflectionMaterialWeight:
                    return "SSR Material Weight";
                case BurtShadingDebugMode.ScreenSpaceReflectionRoughnessMip:
                    return "SSR Roughness Mip";
                case BurtShadingDebugMode.ScreenSpaceReflectionResolvedColor:
                    return "SSR Resolved Color";
                case BurtShadingDebugMode.ScreenSpaceReflectionTemporalAlpha:
                    return "SSR Temporal Alpha";
                case BurtShadingDebugMode.ScreenSpaceReflectionRoughnessMipAlpha:
                    return "SSR Mip Alpha";
                case BurtShadingDebugMode.ScreenSpaceReflectionReceiverContinuity:
                    return "SSR Receiver Continuity";
                case BurtShadingDebugMode.ScreenSpaceReflectionFallbackSpecular:
                    return "SSR Fallback Specular";
                case BurtShadingDebugMode.ScreenSpaceReflectionCompositeDelta:
                    return "SSR Composite Delta";
                case BurtShadingDebugMode.ScreenSpaceReflectionCameraColor:
                    return "SSR Camera Color";
                case BurtShadingDebugMode.ScreenSpaceReflectionDepthQuality:
                    return "SSR Depth Quality";
                case BurtShadingDebugMode.ScreenSpaceReflectionWorldQuality:
                    return "SSR World Quality";
                case BurtShadingDebugMode.ScreenSpaceReflectionResolveQuality:
                    return "SSR Resolve Quality";
                case BurtShadingDebugMode.ScreenSpaceReflectionSurfaceSupport:
                    return "SSR Surface Support";
                case BurtShadingDebugMode.ScreenSpaceReflectionHiZSkipCandidate:
                    return "SSR HiZ Skip Candidate";
                case BurtShadingDebugMode.ScreenSpaceReflectionHiZMipLevel:
                    return "SSR HiZ Mip Level";
                case BurtShadingDebugMode.ScreenSpaceReflectionHiZDivergence:
                    return "SSR HiZ Divergence";
                case BurtShadingDebugMode.ScreenSpaceReflectionHiZMissedHits:
                    return "SSR HiZ Missed Hits";
                case BurtShadingDebugMode.ScreenSpaceReflectionHiZRawHitMiss:
                    return "SSR HiZ Raw Hit Miss";
                case BurtShadingDebugMode.ScreenSpaceReflectionHiZResolvedHitMiss:
                    return "SSR HiZ Resolved Hit Miss";
                case BurtShadingDebugMode.ScreenSpaceReflectionHiZVisibilityMiss:
                    return "SSR HiZ Visibility Miss";
                case BurtShadingDebugMode.ScreenSpaceReflectionHiZSkipUsed:
                    return "SSR HiZ Skip Used";
                case BurtShadingDebugMode.ScreenSpaceReflectionHiZProbeBlocked:
                    return "SSR HiZ Probe Blocked";
                case BurtShadingDebugMode.ScreenSpaceReflectionHiZStepCompare:
                    return "SSR HiZ Step Compare";
                case BurtShadingDebugMode.ScreenSpaceReflectionHiZWorkCompare:
                    return "SSR HiZ Work Compare";
                case BurtShadingDebugMode.ScreenSpaceReflectionHiZStepSaved:
                    return "SSR HiZ Step Saved";
                case BurtShadingDebugMode.TemporalAAHistory:
                    return "TAA History Color"; // 显示 TAA 重投影采样到的 history。
                case BurtShadingDebugMode.TemporalAAFeedback:
                    return "TAA Feedback"; // 显示最终 history 混合权重。
                case BurtShadingDebugMode.TemporalAARejection:
                    return "TAA Rejection Weights"; // 显示 luma / clip / depth 通过权重，亮色不是异常 rejection。
                case BurtShadingDebugMode.TemporalAAHistoryUV:
                    return "TAA History UV"; // 显示重投影后的 history UV 和屏幕内状态。
                case BurtShadingDebugMode.TemporalAADifference:
                    return "TAA Difference"; // 显示当前帧与 history 的颜色差异。
                case BurtShadingDebugMode.TemporalAAVelocity:
                    return "TAA Velocity";
                case BurtShadingDebugMode.TemporalAAConfidence:
                    return "TAA History Availability";
                case BurtShadingDebugMode.TemporalAACurrentDepth:
                    return "TAA Current Depth";
                case BurtShadingDebugMode.TemporalAADepthHistory:
                    return "TAA Depth History";
                case BurtShadingDebugMode.TemporalAADepthDelta:
                    return "TAA Depth Delta";
                case BurtShadingDebugMode.TemporalAACurrentColor:
                    return "TAA Current Color";
                case BurtShadingDebugMode.TemporalAAResolvedColor:
                    return "TAA Resolved Color";
                case BurtShadingDebugMode.TemporalAARawVelocity:
                    return "TAA Raw Velocity";
                case BurtShadingDebugMode.TemporalAAUpdatedConfidence:
                    return "TAA History Acceptance";
                case BurtShadingDebugMode.TemporalAAStaticRelax:
                    return "TAA Depth Continuity";
                case BurtShadingDebugMode.TemporalAALumaRejection:
                    return "TAA Luma Reject";
                case BurtShadingDebugMode.TemporalAAClipRejection:
                    return "TAA Clip Reject";
                case BurtShadingDebugMode.TemporalAADepthRejection:
                    return "TAA Depth Reject";
                case BurtShadingDebugMode.TemporalAANormalRejection:
                    return "TAA Normal Reject";
                case BurtShadingDebugMode.TemporalAAMotionRejection:
                    return "TAA Motion Reject";
                case BurtShadingDebugMode.TemporalAAConfidenceGate:
                    return "TAA Blend Factors";
                case BurtShadingDebugMode.TemporalAAVelocitySource:
                    return "TAA Velocity Source";
                case BurtShadingDebugMode.TemporalAAGBufferNormal:
                    return "TAA GBuffer Normal";
                case BurtShadingDebugMode.TemporalAAParallaxRejection:
                    return "TAA Parallax Rejection";
                case BurtShadingDebugMode.TemporalAAAntiFlicker:
                    return "TAA Stable History";
                case BurtShadingDebugMode.TemporalAAHistoryCoverage:
                    return "TAA History Coverage";
                case BurtShadingDebugMode.TemporalAAPrevUseCount:
                    return "TAA Prev Use Count";
                case BurtShadingDebugMode.TemporalAAResponsiveMask:
                    return "TAA Responsive Mask";
                case BurtShadingDebugMode.TemporalAAMetadata:
                    return "TAA Metadata";
                case BurtShadingDebugMode.TemporalAAObjectMotionMask:
                    return "TAA Object Motion Mask";
                case BurtShadingDebugMode.TemporalAAUpscaleState:
                    return "TAA Upscale State";
                case BurtShadingDebugMode.TemporalAARejectionReasons:
                    return "TAA Rejection Reasons";
                case BurtShadingDebugMode.TemporalAAFeedbackWeight:
                    return "TAA Feedback Weight";
                default:
                    return ObjectNames.NicifyVariableName(mode.ToString()); // 兜底美化 enum 名，避免新增模式显示为空。
            }
        }
    }

    internal sealed class BurtShadingDebugGroup // 一个 Toolbar Dropdown 对应一个 Debug 分类，参考 XRender Groups 目录里的 Material / Lighting / Deferred 分组。
    {
        public BurtShadingDebugGroup(string title, string buttonText, BurtShadingDebugMode[] modes) // 保存分类标题、按钮短名和该分类下的模式列表。
        {
            Title = title; // 弹窗顶部显示的完整分类名。
            ButtonText = buttonText; // Toolbar 收起状态下显示的短名。
            Modes = modes; // 该分类包含的 Debug 模式。
        }

        public string Title { get; } // 分类标题，例如 Material / GBuffer / Lighting。

        public string ButtonText { get; } // Toolbar 按钮默认短名。

        public BurtShadingDebugMode[] Modes { get; } // 当前分类的 Debug 模式列表。

        public int VisibleModeCount
        {
            get
            {
                var count = 0;
                foreach (var mode in Modes)
                {
                    if (BurtShadingDebugSettings.IsImportantMode(mode))
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public bool Contains(BurtShadingDebugMode mode) // 判断当前模式是否属于这个分类，用来高亮对应 Dropdown。
        {
            if (!BurtShadingDebugSettings.IsImportantMode(mode))
            {
                return false;
            }

            foreach (var candidate in Modes) // 遍历数组，保持 Unity 旧版本兼容，不依赖 LINQ。
            {
                if (candidate == mode) // 命中当前模式。
                {
                    return true; // 该 Dropdown 应该显示为选中。
                }
            }

            return false; // 当前模式不属于该分类。
        }
    }

    internal static class BurtShadingDebugGroups // BurtRP 的分类表，结构参考 XRender Editor/XShaderDebug/Groups 的多 Dropdown 注册方式。
    {
        public static readonly BurtShadingDebugGroup General = new BurtShadingDebugGroup("General", "Off", new[] // 通用开关分类，用来快速关闭 Debug。
        {
            BurtShadingDebugMode.None // 正常渲染模式。
        });

        public static readonly BurtShadingDebugGroup Material = new BurtShadingDebugGroup("Material / Generic Data", "Material", new[] // 基础材质输入和 XRender GenericData 派生值。
        {
            BurtShadingDebugMode.Albedo, // BaseMap 与 BaseColor 合成后的基础色。
            BurtShadingDebugMode.DiffuseColor, // metallic 扣除后的 diffuse 颜色。
            BurtShadingDebugMode.NormalWS, // 最终世界空间法线。
            BurtShadingDebugMode.Smoothness, // 面板语义下的光滑度。
            BurtShadingDebugMode.Roughness, // shader 内部使用的感知粗糙度。
            BurtShadingDebugMode.Metallic, // 最终金属度。
            BurtShadingDebugMode.Occlusion, // 最终 AO。
            BurtShadingDebugMode.Height, // Mask Map B height channel.
            BurtShadingDebugMode.Reflectance // XRender reflectance 输入。
        });

        public static readonly BurtShadingDebugGroup GBuffer = new BurtShadingDebugGroup("GBuffer / Deferred Data", "GBuffer", new[] // Deferred 前置检查：验证 GBuffer 编解码和 PBRData 重建。
        {
            BurtShadingDebugMode.GBufferBaseColor, // GBuffer0.rgb 解码结果。
            BurtShadingDebugMode.GBufferNormalWS, // GBuffer1.rg oct direction 解码结果。
            BurtShadingDebugMode.GBufferMetallic, // GBuffer1.b material channel 解码结果。
            BurtShadingDebugMode.GBufferSmoothness, // GBuffer1.a 解码结果。
            BurtShadingDebugMode.GBufferOcclusion, // GBuffer0.a 解码结果。
            BurtShadingDebugMode.GBufferReflectance, // GBuffer2.a 解码结果。
            BurtShadingDebugMode.GBufferRoughness, // GBuffer -> PBRMaterialData 的 roughness。
            BurtShadingDebugMode.GBufferDiffuseColor, // GBuffer -> PBRMaterialData 的 DiffuseColor。
            BurtShadingDebugMode.GBufferHairStrandDirection, // Hair 专用：GBuffer1.rg 解码后的 strand direction。
            BurtShadingDebugMode.GBufferHairScatter, // Hair 专用：GBuffer1.b material channel 解码后的 scatter。
            BurtShadingDebugMode.GBufferHairShift, // Hair 专用：GBuffer1.b material channel 解码后的 shift scale。
            BurtShadingDebugMode.GBufferClearCoatMask,
            BurtShadingDebugMode.GBufferClearCoatNormalWS,
            BurtShadingDebugMode.GBufferClearCoatRoughness,
            BurtShadingDebugMode.GBufferSubsurfaceStrength, // Subsurface 专用：GBuffer1.b material channel 解码后的 strength。
            BurtShadingDebugMode.GBufferSubsurfaceThickness,
            BurtShadingDebugMode.GBufferSubsurfaceProfileIndex,
            BurtShadingDebugMode.GBufferFoliageTransmissionColor,
            BurtShadingDebugMode.GBufferFoliageTransmissionWeight,
            BurtShadingDebugMode.GBufferFoliageThickness,
            BurtShadingDebugMode.GBufferFoliageTransmissionNdotL,
            BurtShadingDebugMode.GBufferFoliageSpecularScale,
            BurtShadingDebugMode.GBufferFoliageScreenSpaceShadowIntensity,
            BurtShadingDebugMode.GBufferAnisotropy,
            BurtShadingDebugMode.GBufferTangentWS
        });

        public static readonly BurtShadingDebugGroup SpecularAA = new BurtShadingDebugGroup("Specular AA / Normal Filtering", "Spec AA", new[] // 对应 XRender Normal Filtering / Anti Aliasing 方向。
        {
            BurtShadingDebugMode.SpecularAARoughness, // 高光实际使用 roughness。
            BurtShadingDebugMode.SpecularAANormalVariance, // 屏幕空间法线方差。
            BurtShadingDebugMode.SpecularAARoughnessDelta // Specular AA 增加量。
        });

        public static readonly BurtShadingDebugGroup DirectBRDF = new BurtShadingDebugGroup("Direct BRDF", "BRDF", new[] // 对应 XRender SlabLobes 的 D / V / F / diffuse lobe 拆分。
        {
            BurtShadingDebugMode.DirectBRDFD, // GGX D 项。
            BurtShadingDebugMode.DirectBRDFVisibility, // Smith Joint Visibility。
            BurtShadingDebugMode.DirectBRDFFresnel, // Schlick Fresnel。
            BurtShadingDebugMode.DirectDiffuseLobe, // diffuse lobe。
            BurtShadingDebugMode.DirectDiffuseBRDF, // 直接 diffuse BRDF。
            BurtShadingDebugMode.DirectSpecularBRDF // 直接 specular BRDF。
        });

        public static readonly BurtShadingDebugGroup Hair = new BurtShadingDebugGroup("Hair Lobes", "Hair", new[] // Hair 专用拆项，方便调 primary/secondary/transmission 近似。
        {
            BurtShadingDebugMode.HairPrimaryLobe, // Hair R/Primary 高光 lobe。
            BurtShadingDebugMode.HairSecondaryLobe, // Hair TT/Secondary 彩色高光 lobe。
            BurtShadingDebugMode.HairTransmissionLobe, // Hair 背光透射近似 lobe。
            BurtShadingDebugMode.HairScatter, // Hair 当前参与 lighting 的 scatter。
            BurtShadingDebugMode.HairAdditionalLighting // Hair 追加光直接贡献。
        });

        public static readonly BurtShadingDebugGroup Subsurface = new BurtShadingDebugGroup("Subsurface", "SSS", new[]
        {
            BurtShadingDebugMode.SubsurfaceProfileId,
            BurtShadingDebugMode.SubsurfaceTransmission,
            BurtShadingDebugMode.SubsurfaceDirectTransmission,
            BurtShadingDebugMode.SubsurfaceTransmissionBRDF,
            BurtShadingDebugMode.SubsurfaceTransmissionShadow,
            BurtShadingDebugMode.SubsurfaceTransmissionPhase,
            BurtShadingDebugMode.SubsurfaceTransmissionThickness,
            BurtShadingDebugMode.SubsurfaceKernelWeight,
            BurtShadingDebugMode.SubsurfaceIndirect
        });

        public static readonly BurtShadingDebugGroup IBL = new BurtShadingDebugGroup("IBL / Energy / Occlusion", "IBL", new[] // 归档环境光、能量守恒和 specular occlusion 相关调试。
        {
            BurtShadingDebugMode.IndirectSpecularDFG, // PreIntegratedFG DFG.xy。
            BurtShadingDebugMode.IndirectSpecularEnvBRDF, // DFG 应用到 F0/F90 后的环境 BRDF。
            BurtShadingDebugMode.SpecularEnergyCompensation, // 直接高光补能。
            BurtShadingDebugMode.IndirectSpecularEnergyCompensation, // 间接高光补能。
            BurtShadingDebugMode.EnergyPreservation, // diffuse 底层保能比例。
            BurtShadingDebugMode.SpecularOcclusion // 环境高光遮蔽。
        });

        public static readonly BurtShadingDebugGroup Lighting = new BurtShadingDebugGroup("Direct Lighting", "Direct", new[] // 直接光照相关拆项。
        {
            BurtShadingDebugMode.DetailLighting, // 中灰 BaseColor 下重新观察光照细节。
            BurtShadingDebugMode.DirectDiffuse, // 直接漫反射最终贡献。
            BurtShadingDebugMode.DirectSpecular // 直接高光最终贡献。
        });

        public static readonly BurtShadingDebugGroup LightingAdditional = new BurtShadingDebugGroup("Additional Lights / Tiling", "Additional", new[] // 追加光和 tiled light 调试。
        {
            BurtShadingDebugMode.AdditionalLighting, // 追加光直接贡献总和。
            BurtShadingDebugMode.AdditionalLightingUnshadowed, // 追加光不乘 additional shadow attenuation 的直接贡献总和。
            BurtShadingDebugMode.AdditionalDiffuse, // 追加光漫反射贡献。
            BurtShadingDebugMode.AdditionalSpecular, // 追加光高光贡献。
            BurtShadingDebugMode.TileLightCount, // Tiled debug：每个 tile 命中的追加光数量。
            BurtShadingDebugMode.TileLightOccupancy, // Tiled debug：tile list 使用率。
            BurtShadingDebugMode.ClusterLightCount,
            BurtShadingDebugMode.ClusterLightOccupancy
        });

        public static readonly BurtShadingDebugGroup LightingIndirect = new BurtShadingDebugGroup("Indirect / Output", "Indirect", new[] // 间接光、AO、自发光和最终输出。
        {
            BurtShadingDebugMode.IndirectLighting, // 间接光总和。
            BurtShadingDebugMode.IndirectDiffuse, // 间接漫反射。
            BurtShadingDebugMode.IndirectSpecular, // 间接高光。
            BurtShadingDebugMode.AmbientOcclusion, // 参与 lighting 的 AO。
            BurtShadingDebugMode.Emission, // 自发光贡献。
            BurtShadingDebugMode.FinalLighting // PBR 光照加自发光后的最终材质颜色。
        });

        public static readonly BurtShadingDebugGroup Shadow = new BurtShadingDebugGroup("Shadow", "Shadow", new[] // Shadow 独立分类，避免和 Lighting / Fullscreen 调试混在一起。
        {
            BurtShadingDebugMode.ShadowAttenuation, // 材质着色时实际使用的主光阴影衰减。
            BurtShadingDebugMode.AdditionalShadowAttenuation, // 材质着色时实际使用的追加光阴影衰减。
            BurtShadingDebugMode.AdditionalShadowFace,
            BurtShadingDebugMode.AdditionalShadowUV,
            BurtShadingDebugMode.AdditionalShadowDepth,
            BurtShadingDebugMode.AdditionalShadowDepthDelta,
            BurtShadingDebugMode.ShadowCascadeIndex, // CSM cascade 命中颜色。
            BurtShadingDebugMode.ShadowCascadeBlend, // CSM cascade 边界混合权重。
            BurtShadingDebugMode.ShadowDistanceFade, // 最后一级 cascade 到无阴影的远距离 fade。
            BurtShadingDebugMode.ShadowPCSSRadius, // 当前像素的 PCSS 半影半径。
            BurtShadingDebugMode.ShadowReceiverDepthDelta, // receiver / shadow map 深度差，用来调 bias。
            BurtShadingDebugMode.ShadowPCSSBlockerFraction, // PCSS blocker search 命中的 blocker 占比。
            BurtShadingDebugMode.MainLightShadow, // 主光 shadow map 全屏 Debug。
            BurtShadingDebugMode.PerObjectShadowObjectIndex,
            BurtShadingDebugMode.PerObjectShadowSlice,
            BurtShadingDebugMode.PerObjectShadowUV,
            BurtShadingDebugMode.PerObjectShadowDepth,
            BurtShadingDebugMode.PerObjectShadowCompare,
            BurtShadingDebugMode.PerObjectShadowTransmissionDepth,
            BurtShadingDebugMode.PerObjectShadowTransmissionThickness,
            BurtShadingDebugMode.PerObjectShadowAtlas
        });

        public static readonly BurtShadingDebugGroup Fullscreen = new BurtShadingDebugGroup("Fullscreen / Render Data", "Fullscreen", new[] // 非后处理的全屏 Render Data 入口。
        {
            BurtShadingDebugMode.CameraDepth // CameraDepth 全屏 Debug。
        });

        public static readonly BurtShadingDebugGroup PostProcess = new BurtShadingDebugGroup("Post Process", "Post FX", new[] // SSAO、SSR、TAA、Bloom、Auto Exposure 等后处理相关调试统一入口。
        {
            BurtShadingDebugMode.ScreenSpaceAmbientOcclusionRaw,
            BurtShadingDebugMode.ScreenSpaceAmbientOcclusionFinal,
            BurtShadingDebugMode.ScreenSpaceAmbientOcclusionOverlay,
            BurtShadingDebugMode.ScreenSpaceAmbientOcclusionHistory,
            BurtShadingDebugMode.ScreenSpaceAmbientOcclusionDifference,
            BurtShadingDebugMode.ScreenSpaceAmbientOcclusionDepthValidity,
            BurtShadingDebugMode.ScreenSpaceAmbientOcclusionSurfaceStability,
            BurtShadingDebugMode.ScreenSpaceAmbientOcclusionDiagnosticCompare,
            BurtShadingDebugMode.ScreenSpaceGlobalIlluminationRaw,
            BurtShadingDebugMode.ScreenSpaceGlobalIlluminationFinal,
            BurtShadingDebugMode.ScreenSpaceGlobalIlluminationHitRatio,
            BurtShadingDebugMode.ScreenSpaceGlobalIlluminationOverlay,
            BurtShadingDebugMode.ScreenSpaceGlobalIlluminationComposite,
            BurtShadingDebugMode.ScreenSpaceGlobalIlluminationTemporalConfidence,
            BurtShadingDebugMode.ScreenSpaceGlobalIlluminationTemporalRejection,
            BurtShadingDebugMode.ScreenSpaceGlobalIlluminationHistory,
            BurtShadingDebugMode.ScreenSpaceGlobalIlluminationDifference,
            BurtShadingDebugMode.ScreenSpaceGlobalIlluminationLeakGuard,
            BurtShadingDebugMode.ScreenSpaceGlobalIlluminationDiagnosticCompare,
            BurtShadingDebugMode.ScreenSpaceGlobalIlluminationConfidence,
            BurtShadingDebugMode.BloomPrefilter,
            BurtShadingDebugMode.BloomFinalBloom,
            BurtShadingDebugMode.BloomMip1,
            BurtShadingDebugMode.BloomMip2,
            BurtShadingDebugMode.BloomMip3,
            BurtShadingDebugMode.BloomMip4,
            BurtShadingDebugMode.BloomMip5,
            BurtShadingDebugMode.BloomAlpha,
            BurtShadingDebugMode.BloomThresholdMask,
            BurtShadingDebugMode.AutoExposureLuminance,
            BurtShadingDebugMode.AutoExposureMeteringWeight,
            BurtShadingDebugMode.AutoExposureHistogramRange,
            BurtShadingDebugMode.ScreenSpaceReflectionRawHitMask,
            BurtShadingDebugMode.ScreenSpaceReflectionHitMask,
            BurtShadingDebugMode.ScreenSpaceReflectionHitUV,
            BurtShadingDebugMode.ScreenSpaceReflectionStepCount,
            BurtShadingDebugMode.ScreenSpaceReflectionColor,
            BurtShadingDebugMode.ScreenSpaceReflectionConfidence,
            BurtShadingDebugMode.ScreenSpaceReflectionDepthDelta,
            BurtShadingDebugMode.ScreenSpaceReflectionWorldError,
            BurtShadingDebugMode.ScreenSpaceReflectionDenoisedColor,
            BurtShadingDebugMode.ScreenSpaceReflectionTemporalColor,
            BurtShadingDebugMode.ScreenSpaceReflectionResolveAlpha,
            BurtShadingDebugMode.ScreenSpaceReflectionVisibilityAlpha,
            BurtShadingDebugMode.ScreenSpaceReflectionMaterialWeight,
            BurtShadingDebugMode.ScreenSpaceReflectionRoughnessMip,
            BurtShadingDebugMode.ScreenSpaceReflectionResolvedColor,
            BurtShadingDebugMode.ScreenSpaceReflectionTemporalAlpha,
            BurtShadingDebugMode.ScreenSpaceReflectionRoughnessMipAlpha,
            BurtShadingDebugMode.ScreenSpaceReflectionReceiverContinuity,
            BurtShadingDebugMode.ScreenSpaceReflectionFallbackSpecular,
            BurtShadingDebugMode.ScreenSpaceReflectionCompositeDelta,
            BurtShadingDebugMode.ScreenSpaceReflectionCameraColor,
            BurtShadingDebugMode.ScreenSpaceReflectionDepthQuality,
            BurtShadingDebugMode.ScreenSpaceReflectionWorldQuality,
            BurtShadingDebugMode.ScreenSpaceReflectionResolveQuality,
            BurtShadingDebugMode.ScreenSpaceReflectionSurfaceSupport,
            BurtShadingDebugMode.ScreenSpaceReflectionHiZSkipCandidate,
            BurtShadingDebugMode.ScreenSpaceReflectionHiZMipLevel,
            BurtShadingDebugMode.ScreenSpaceReflectionHiZDivergence,
            BurtShadingDebugMode.ScreenSpaceReflectionHiZMissedHits,
            BurtShadingDebugMode.ScreenSpaceReflectionHiZRawHitMiss,
            BurtShadingDebugMode.ScreenSpaceReflectionHiZResolvedHitMiss,
            BurtShadingDebugMode.ScreenSpaceReflectionHiZVisibilityMiss,
            BurtShadingDebugMode.ScreenSpaceReflectionHiZSkipUsed,
            BurtShadingDebugMode.ScreenSpaceReflectionHiZProbeBlocked,
            BurtShadingDebugMode.ScreenSpaceReflectionHiZStepCompare,
            BurtShadingDebugMode.ScreenSpaceReflectionHiZWorkCompare,
            BurtShadingDebugMode.ScreenSpaceReflectionHiZStepSaved,
            BurtShadingDebugMode.TemporalAAHistory,
            BurtShadingDebugMode.TemporalAAFeedback,
            BurtShadingDebugMode.TemporalAARejection,
            BurtShadingDebugMode.TemporalAARejectionReasons,
            BurtShadingDebugMode.TemporalAAHistoryUV,
            BurtShadingDebugMode.TemporalAADifference,
            BurtShadingDebugMode.TemporalAAVelocity,
            BurtShadingDebugMode.TemporalAAConfidence,
            BurtShadingDebugMode.TemporalAACurrentDepth,
            BurtShadingDebugMode.TemporalAADepthHistory,
            BurtShadingDebugMode.TemporalAADepthDelta,
            BurtShadingDebugMode.TemporalAAParallaxRejection,
            BurtShadingDebugMode.TemporalAAFeedbackWeight,
            BurtShadingDebugMode.TemporalAACurrentColor,
            BurtShadingDebugMode.TemporalAAResolvedColor,
            BurtShadingDebugMode.TemporalAARawVelocity,
            BurtShadingDebugMode.TemporalAAUpdatedConfidence,
            BurtShadingDebugMode.TemporalAAStaticRelax,
            BurtShadingDebugMode.TemporalAALumaRejection,
            BurtShadingDebugMode.TemporalAAClipRejection,
            BurtShadingDebugMode.TemporalAADepthRejection,
            BurtShadingDebugMode.TemporalAANormalRejection,
            BurtShadingDebugMode.TemporalAAMotionRejection,
            BurtShadingDebugMode.TemporalAAConfidenceGate,
            BurtShadingDebugMode.TemporalAAVelocitySource,
            BurtShadingDebugMode.TemporalAAGBufferNormal,
            BurtShadingDebugMode.TemporalAAAntiFlicker,
            BurtShadingDebugMode.TemporalAAHistoryCoverage,
            BurtShadingDebugMode.TemporalAAPrevUseCount,
            BurtShadingDebugMode.TemporalAAResponsiveMask,
            BurtShadingDebugMode.TemporalAAMetadata,
            BurtShadingDebugMode.TemporalAAObjectMotionMask,
            BurtShadingDebugMode.TemporalAAUpscaleState
        });

        public static readonly BurtShadingDebugGroup ScreenSpaceAmbientOcclusion = new BurtShadingDebugGroup("Screen Space Ambient Occlusion", "SSAO", new[] // SSAO 后处理调试。
        {
            BurtShadingDebugMode.ScreenSpaceAmbientOcclusionRaw,
            BurtShadingDebugMode.ScreenSpaceAmbientOcclusionFinal,
            BurtShadingDebugMode.ScreenSpaceAmbientOcclusionOverlay,
            BurtShadingDebugMode.ScreenSpaceAmbientOcclusionHistory,
            BurtShadingDebugMode.ScreenSpaceAmbientOcclusionDifference,
            BurtShadingDebugMode.ScreenSpaceAmbientOcclusionDepthValidity,
            BurtShadingDebugMode.ScreenSpaceAmbientOcclusionSurfaceStability,
            BurtShadingDebugMode.ScreenSpaceAmbientOcclusionDiagnosticCompare
        });

        public static readonly BurtShadingDebugGroup ScreenSpaceShadow = new BurtShadingDebugGroup("Screen Space Shadow", "SS Shadow", new[]
        {
            BurtShadingDebugMode.ScreenSpaceShadow
        });

        public static readonly BurtShadingDebugGroup ScreenSpaceGlobalIllumination = new BurtShadingDebugGroup("Screen Space Global Illumination", "BurtGI", new[]
        {
            BurtShadingDebugMode.ScreenSpaceGlobalIlluminationRaw,
            BurtShadingDebugMode.ScreenSpaceGlobalIlluminationFinal,
            BurtShadingDebugMode.ScreenSpaceGlobalIlluminationHitRatio,
            BurtShadingDebugMode.ScreenSpaceGlobalIlluminationOverlay,
            BurtShadingDebugMode.ScreenSpaceGlobalIlluminationComposite,
            BurtShadingDebugMode.ScreenSpaceGlobalIlluminationTemporalConfidence,
            BurtShadingDebugMode.ScreenSpaceGlobalIlluminationTemporalRejection,
            BurtShadingDebugMode.ScreenSpaceGlobalIlluminationHistory,
            BurtShadingDebugMode.ScreenSpaceGlobalIlluminationDifference,
            BurtShadingDebugMode.ScreenSpaceGlobalIlluminationLeakGuard,
            BurtShadingDebugMode.ScreenSpaceGlobalIlluminationDiagnosticCompare,
            BurtShadingDebugMode.ScreenSpaceGlobalIlluminationConfidence
        });

        public static readonly BurtShadingDebugGroup ScreenSpaceSubsurface = new BurtShadingDebugGroup("Screen Space Subsurface", "SSS", new[]
        {
            BurtShadingDebugMode.ScreenSpaceSubsurfaceSetup,
            BurtShadingDebugMode.ScreenSpaceSubsurfaceMask,
            BurtShadingDebugMode.ScreenSpaceSubsurfaceBlur,
            BurtShadingDebugMode.ScreenSpaceSubsurfaceCombine,
            BurtShadingDebugMode.ScreenSpaceSubsurfaceProfileTintRaw,
            BurtShadingDebugMode.ScreenSpaceSubsurfaceProfileTintedFinal,
            BurtShadingDebugMode.ScreenSpaceSubsurfaceBlurAlpha,
            BurtShadingDebugMode.ScreenSpaceSubsurfaceBlurRadius,
            BurtShadingDebugMode.ScreenSpaceSubsurfaceBlurDelta,
            BurtShadingDebugMode.ScreenSpaceSubsurfaceSeparableValidity,
            BurtShadingDebugMode.ScreenSpaceSubsurfaceSeparableIO,
            BurtShadingDebugMode.ScreenSpaceSubsurfaceSeparableChain,
            BurtShadingDebugMode.ScreenSpaceSubsurfaceAlgorithm,
            BurtShadingDebugMode.ScreenSpaceSubsurfaceHistory
        });

        public static readonly BurtShadingDebugGroup FurBlur = new BurtShadingDebugGroup("Fur Blur", "Fur", new[]
        {
            BurtShadingDebugMode.FurBlurDirection,
            BurtShadingDebugMode.FurBlurPropertyDepth,
            BurtShadingDebugMode.FurBlurCurrent,
            BurtShadingDebugMode.FurBlurTemporal,
            BurtShadingDebugMode.FurBlurHistory,
            BurtShadingDebugMode.FurBlurDiagnostic
        });

        public static readonly BurtShadingDebugGroup Bloom = new BurtShadingDebugGroup("Bloom", "Bloom", new[] // Bloom 后处理调试。
        {
            BurtShadingDebugMode.BloomPrefilter,
            BurtShadingDebugMode.BloomFinalBloom,
            BurtShadingDebugMode.BloomMip1,
            BurtShadingDebugMode.BloomMip2,
            BurtShadingDebugMode.BloomMip3,
            BurtShadingDebugMode.BloomMip4,
            BurtShadingDebugMode.BloomMip5,
            BurtShadingDebugMode.BloomAlpha,
            BurtShadingDebugMode.BloomThresholdMask
        });

        public static readonly BurtShadingDebugGroup AutoExposure = new BurtShadingDebugGroup("Auto Exposure", "Exposure", new[] // 自动曝光后处理调试。
        {
            BurtShadingDebugMode.AutoExposureLuminance,
            BurtShadingDebugMode.AutoExposureMeteringWeight,
            BurtShadingDebugMode.AutoExposureHistogramRange
        });

        public static readonly BurtShadingDebugGroup Atmosphere = new BurtShadingDebugGroup("Atmosphere", "Atmosphere", new[]
        {
            BurtShadingDebugMode.Atmosphere,
            BurtShadingDebugMode.AtmosphereRayleigh,
            BurtShadingDebugMode.AtmosphereMie,
            BurtShadingDebugMode.AtmosphereTransmittance,
            BurtShadingDebugMode.AtmosphereAerialTransmittance,
            BurtShadingDebugMode.AtmosphereAerialInscatter,
            BurtShadingDebugMode.AtmosphereAerialFogAmount,
            BurtShadingDebugMode.AtmosphereAerialHeightFade,
            BurtShadingDebugMode.AtmosphereAerialSummary,
            BurtShadingDebugMode.AtmosphereSunDisk,
            BurtShadingDebugMode.AtmosphereSunHalo,
            BurtShadingDebugMode.AtmosphereHorizon,
            BurtShadingDebugMode.AtmosphereGroundBlend,
            BurtShadingDebugMode.AtmosphereViewDirection
        });

        public static readonly BurtShadingDebugGroup Fog = new BurtShadingDebugGroup("Fog", "Fog", new[]
        {
            BurtShadingDebugMode.FogAmount,
            BurtShadingDebugMode.FogTransmittance,
            BurtShadingDebugMode.FogHeight,
            BurtShadingDebugMode.FogDistance
        });

        public static readonly BurtShadingDebugGroup VolumetricFog = new BurtShadingDebugGroup("Volumetric Fog", "Vol Fog", new[]
        {
            BurtShadingDebugMode.VolumetricFogScattering,
            BurtShadingDebugMode.VolumetricFogTransmittance,
            BurtShadingDebugMode.VolumetricFogDensity,
            BurtShadingDebugMode.VolumetricFogDistance,
            BurtShadingDebugMode.VolumetricFogStepCount
        });

        public static readonly BurtShadingDebugGroup ScreenSpaceReflections = new BurtShadingDebugGroup("Screen Space Reflections", "SSR", new[] // SSR 独立分类，避免挤在通用 Fullscreen 调试里。
        {
            BurtShadingDebugMode.ScreenSpaceReflectionRawHitMask,
            BurtShadingDebugMode.ScreenSpaceReflectionHitMask, // SSR 最终遮罩。
            BurtShadingDebugMode.ScreenSpaceReflectionHitUV, // SSR 命中 UV。
            BurtShadingDebugMode.ScreenSpaceReflectionStepCount, // SSR raymarch 步数。
            BurtShadingDebugMode.ScreenSpaceReflectionColor, // SSR 采样到的反射颜色。
            BurtShadingDebugMode.ScreenSpaceReflectionConfidence,
            BurtShadingDebugMode.ScreenSpaceReflectionDepthDelta,
            BurtShadingDebugMode.ScreenSpaceReflectionWorldError,
            BurtShadingDebugMode.ScreenSpaceReflectionDenoisedColor,
            BurtShadingDebugMode.ScreenSpaceReflectionTemporalColor,
            BurtShadingDebugMode.ScreenSpaceReflectionResolveAlpha,
            BurtShadingDebugMode.ScreenSpaceReflectionVisibilityAlpha,
            BurtShadingDebugMode.ScreenSpaceReflectionMaterialWeight,
            BurtShadingDebugMode.ScreenSpaceReflectionRoughnessMip,
            BurtShadingDebugMode.ScreenSpaceReflectionResolvedColor,
            BurtShadingDebugMode.ScreenSpaceReflectionTemporalAlpha,
            BurtShadingDebugMode.ScreenSpaceReflectionRoughnessMipAlpha,
            BurtShadingDebugMode.ScreenSpaceReflectionReceiverContinuity,
            BurtShadingDebugMode.ScreenSpaceReflectionFallbackSpecular,
            BurtShadingDebugMode.ScreenSpaceReflectionCompositeDelta,
            BurtShadingDebugMode.ScreenSpaceReflectionCameraColor,
            BurtShadingDebugMode.ScreenSpaceReflectionDepthQuality,
            BurtShadingDebugMode.ScreenSpaceReflectionWorldQuality,
            BurtShadingDebugMode.ScreenSpaceReflectionResolveQuality,
            BurtShadingDebugMode.ScreenSpaceReflectionSurfaceSupport,
            BurtShadingDebugMode.ScreenSpaceReflectionHiZSkipCandidate,
            BurtShadingDebugMode.ScreenSpaceReflectionHiZMipLevel,
            BurtShadingDebugMode.ScreenSpaceReflectionHiZDivergence,
            BurtShadingDebugMode.ScreenSpaceReflectionHiZMissedHits,
            BurtShadingDebugMode.ScreenSpaceReflectionHiZRawHitMiss,
            BurtShadingDebugMode.ScreenSpaceReflectionHiZResolvedHitMiss,
            BurtShadingDebugMode.ScreenSpaceReflectionHiZVisibilityMiss,
            BurtShadingDebugMode.ScreenSpaceReflectionHiZSkipUsed,
            BurtShadingDebugMode.ScreenSpaceReflectionHiZProbeBlocked,
            BurtShadingDebugMode.ScreenSpaceReflectionHiZStepCompare,
            BurtShadingDebugMode.ScreenSpaceReflectionHiZWorkCompare,
            BurtShadingDebugMode.ScreenSpaceReflectionHiZStepSaved
        });

        public static readonly BurtShadingDebugGroup TemporalAA = new BurtShadingDebugGroup("Temporal AA", "TAA", new[] // TAA 独立分类，避免和通用 Fullscreen 调试混在一起。
        {
            BurtShadingDebugMode.TemporalAAHistory,
            BurtShadingDebugMode.TemporalAAFeedback,
            BurtShadingDebugMode.TemporalAARejection,
            BurtShadingDebugMode.TemporalAARejectionReasons,
            BurtShadingDebugMode.TemporalAAHistoryUV,
            BurtShadingDebugMode.TemporalAADifference,
            BurtShadingDebugMode.TemporalAAVelocity,
            BurtShadingDebugMode.TemporalAAConfidence,
            BurtShadingDebugMode.TemporalAACurrentDepth,
            BurtShadingDebugMode.TemporalAADepthHistory,
            BurtShadingDebugMode.TemporalAADepthDelta,
            BurtShadingDebugMode.TemporalAAParallaxRejection,
            BurtShadingDebugMode.TemporalAAFeedbackWeight,
            BurtShadingDebugMode.TemporalAACurrentColor,
            BurtShadingDebugMode.TemporalAAResolvedColor,
            BurtShadingDebugMode.TemporalAARawVelocity,
            BurtShadingDebugMode.TemporalAAUpdatedConfidence,
            BurtShadingDebugMode.TemporalAAStaticRelax,
            BurtShadingDebugMode.TemporalAALumaRejection,
            BurtShadingDebugMode.TemporalAAClipRejection,
            BurtShadingDebugMode.TemporalAADepthRejection,
            BurtShadingDebugMode.TemporalAANormalRejection,
            BurtShadingDebugMode.TemporalAAMotionRejection,
            BurtShadingDebugMode.TemporalAAConfidenceGate,
            BurtShadingDebugMode.TemporalAAVelocitySource,
            BurtShadingDebugMode.TemporalAAGBufferNormal,
            BurtShadingDebugMode.TemporalAAAntiFlicker,
            BurtShadingDebugMode.TemporalAAHistoryCoverage,
            BurtShadingDebugMode.TemporalAAPrevUseCount,
            BurtShadingDebugMode.TemporalAAResponsiveMask,
            BurtShadingDebugMode.TemporalAAMetadata,
            BurtShadingDebugMode.TemporalAAObjectMotionMask,
            BurtShadingDebugMode.TemporalAAUpscaleState
        });
    }

    [Overlay(typeof(SceneView), "Burt Shading Debug")] // 在 SceneView 注册 BurtRP Shading Debug Overlay。
    internal sealed class BurtShadingDebugOverlay : ToolbarOverlay // 组合多个分类 Dropdown，参考 XRender XShaderDebugOverlay 的多按钮结构。
    {
        public BurtShadingDebugOverlay() // Unity 创建 Overlay 时会调用这个构造函数。
            : base(
                BurtShadingDebugFullscreenDropdown.Id,
                BurtShadingDebugAtmosphereDropdown.Id,
                BurtShadingDebugFogDropdown.Id,
                BurtShadingDebugVolumetricFogDropdown.Id) // 每个 ID 对应一个 EditorToolbarElement。
        {
        }
    }

    [Overlay(typeof(SceneView), "Burt Material Debug")] // 在 SceneView Overlays 菜单中单独注册材质调试入口。
    internal sealed class BurtMaterialDebugOverlay : ToolbarOverlay
    {
        public BurtMaterialDebugOverlay()
            : base(
                BurtShadingDebugMaterialDropdown.Id,
                BurtShadingDebugGBufferDropdown.Id,
                BurtShadingDebugSpecularAADropdown.Id,
                BurtShadingDebugBRDFDropdown.Id,
                BurtShadingDebugHairDropdown.Id,
                BurtShadingDebugSubsurfaceDropdown.Id,
                BurtShadingDebugIBLDropdown.Id)
        {
        }
    }

    [Overlay(typeof(SceneView), "Burt Lighting Debug")] // 在 SceneView Overlays 菜单中单独注册光照调试入口。
    internal sealed class BurtLightingDebugOverlay : ToolbarOverlay
    {
        public BurtLightingDebugOverlay()
            : base(
                BurtShadingDebugLightingDropdown.Id,
                BurtShadingDebugAdditionalLightingDropdown.Id,
                BurtShadingDebugIndirectLightingDropdown.Id,
                BurtShadingDebugShadowDropdown.Id)
        {
        }
    }

    [Overlay(typeof(SceneView), "Burt Post Process Debug")] // 在 SceneView Overlays 菜单中单独注册后处理调试入口。
    internal sealed class BurtPostProcessDebugOverlay : ToolbarOverlay
    {
        public BurtPostProcessDebugOverlay()
            : base(
                BurtShadingDebugSSAODropdown.Id,
                BurtShadingDebugScreenSpaceShadowDropdown.Id,
                BurtShadingDebugBurtGIDropdown.Id,
                BurtShadingDebugSSSDropdown.Id,
                BurtShadingDebugFurBlurDropdown.Id,
                BurtShadingDebugBloomDropdown.Id,
                BurtShadingDebugAutoExposureDropdown.Id,
                BurtShadingDebugSSRDropdown.Id,
                BurtShadingDebugTemporalAADropdown.Id)
        {
        }
    }

    internal abstract class BurtShadingDebugGroupDropdown : EditorToolbarDropdownToggle, IAccessContainerWindow // 所有分类 Dropdown 的公共基类。
    {
        private static readonly List<BurtShadingDebugGroupDropdown> Instances = new List<BurtShadingDebugGroupDropdown>(); // 记录已挂载的按钮，便于切换模式后一起刷新。

        private readonly BurtShadingDebugGroup group; // 当前 Dropdown 对应的 Debug 分类。

        public EditorWindow containerWindow { get; set; } // IAccessContainerWindow 要求暴露宿主窗口引用。

        protected BurtShadingDebugGroupDropdown(BurtShadingDebugGroup group) // 子类只需要传入对应分类。
        {
            this.group = group; // 保存分类数据，后续弹窗和高亮都用它。
            tooltip = "BurtRP " + group.Title + " Debug"; // 鼠标悬停时显示完整分类说明。
            UpdateVisualState(); // 初始化按钮文字和 Toggle 状态。
            dropdownClicked += () => UnityEditor.PopupWindow.Show(worldBound, new BurtShadingDebugPopup(group)); // 打开只包含该分类的弹窗。
            RegisterCallback<AttachToPanelEvent>(_ => RegisterInstance()); // 挂载到 SceneView 时登记实例。
            RegisterCallback<DetachFromPanelEvent>(_ => Instances.Remove(this)); // 从 SceneView 移除时解除登记，避免保留失效引用。
            this.RegisterValueChangedCallback(OnToggleValueChanged); // 点击已激活的工具栏按钮时切回正常画面。
        }

        public void UpdateVisualState() // 根据全局 Debug 模式刷新按钮显示。
        {
            var mode = BurtShadingDebugSettings.Mode; // 读取当前选中的 Debug 模式。
            bool isActiveGroup = group.Contains(mode); // 当前模式属于该分类时高亮该 Dropdown。
            SetValueWithoutNotify(isActiveGroup); // 让 Toolbar Toggle 视觉上反映当前分类，避免刷新状态时触发关闭逻辑。
            text = isActiveGroup && mode != BurtShadingDebugMode.None ? BurtShadingDebugDisplayNames.GetDisplayName(mode) : group.ButtonText; // 激活分类显示具体模式，未激活时显示分类名。
        }

        public static void UpdateAllVisualStates() // 外部切换模式后调用，刷新所有已创建的 Dropdown。
        {
            foreach (var instance in Instances) // 遍历 SceneView 中的所有 BurtRP Shading Debug 按钮。
            {
                instance.UpdateVisualState(); // 同步按钮状态和文字。
            }
        }

        private void RegisterInstance() // 记录当前 Dropdown 实例。
        {
            if (!Instances.Contains(this)) // Unity 面板重挂载时可能重复回调，需要去重。
            {
                Instances.Add(this); // 保存实例供全局刷新使用。
            }

            UpdateVisualState(); // 挂载后再刷新一次，处理域重载后的状态恢复。
        }

        private void OnToggleValueChanged(ChangeEvent<bool> evt) // 处理用户直接点击 toolbar 按钮本体的开关行为。
        {
            if (evt.newValue || !group.Contains(BurtShadingDebugSettings.Mode)) // 未激活分类被点亮时没有明确模式可切，交回 popup 选择。
            {
                UpdateVisualState(); // 还原为当前真实状态，避免按钮假亮。
                return;
            }

            BurtShadingDebugOverlayUtility.SetMode(BurtShadingDebugMode.None); // 已激活分类再次点击时关闭 Debug。
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))] // 注册 Material 分类按钮。
    internal sealed class BurtShadingDebugMaterialDropdown : BurtShadingDebugGroupDropdown
    {
        public const string Id = "BurtRP/Shading Debug/Material"; // ToolbarOverlay 引用的唯一 ID。

        public BurtShadingDebugMaterialDropdown() // Unity 通过无参构造创建 ToolbarElement。
            : base(BurtShadingDebugGroups.Material) // 绑定基础材质分类。
        {
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))] // 注册 GBuffer 分类按钮。
    internal sealed class BurtShadingDebugGBufferDropdown : BurtShadingDebugGroupDropdown
    {
        public const string Id = "BurtRP/Shading Debug/GBuffer"; // ToolbarOverlay 引用的唯一 ID。

        public BurtShadingDebugGBufferDropdown() // Unity 通过无参构造创建 ToolbarElement。
            : base(BurtShadingDebugGroups.GBuffer) // 绑定 GBuffer / Deferred Data 分类。
        {
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))] // 注册 Specular AA 分类按钮。
    internal sealed class BurtShadingDebugSpecularAADropdown : BurtShadingDebugGroupDropdown
    {
        public const string Id = "BurtRP/Shading Debug/SpecularAA"; // ToolbarOverlay 引用的唯一 ID。

        public BurtShadingDebugSpecularAADropdown() // Unity 通过无参构造创建 ToolbarElement。
            : base(BurtShadingDebugGroups.SpecularAA) // 绑定 Specular AA 分类。
        {
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))] // 注册 Direct BRDF 分类按钮。
    internal sealed class BurtShadingDebugBRDFDropdown : BurtShadingDebugGroupDropdown
    {
        public const string Id = "BurtRP/Shading Debug/BRDF"; // ToolbarOverlay 引用的唯一 ID。

        public BurtShadingDebugBRDFDropdown() // Unity 通过无参构造创建 ToolbarElement。
            : base(BurtShadingDebugGroups.DirectBRDF) // 绑定 Direct BRDF 分类。
        {
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))] // 注册 Hair 分类按钮。
    internal sealed class BurtShadingDebugHairDropdown : BurtShadingDebugGroupDropdown
    {
        public const string Id = "BurtRP/Shading Debug/Hair"; // ToolbarOverlay 引用的唯一 ID。

        public BurtShadingDebugHairDropdown() // Unity 通过无参构造创建 ToolbarElement。
            : base(BurtShadingDebugGroups.Hair) // 绑定 Hair lobes 分类。
        {
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))]
    internal sealed class BurtShadingDebugSubsurfaceDropdown : BurtShadingDebugGroupDropdown
    {
        public const string Id = "BurtRP/Shading Debug/Subsurface";

        public BurtShadingDebugSubsurfaceDropdown()
            : base(BurtShadingDebugGroups.Subsurface)
        {
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))] // 注册 IBL 分类按钮。
    internal sealed class BurtShadingDebugIBLDropdown : BurtShadingDebugGroupDropdown
    {
        public const string Id = "BurtRP/Shading Debug/IBL"; // ToolbarOverlay 引用的唯一 ID。

        public BurtShadingDebugIBLDropdown() // Unity 通过无参构造创建 ToolbarElement。
            : base(BurtShadingDebugGroups.IBL) // 绑定 IBL / Energy / Occlusion 分类。
        {
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))] // 注册 Lighting 分类按钮。
    internal sealed class BurtShadingDebugLightingDropdown : BurtShadingDebugGroupDropdown
    {
        public const string Id = "BurtRP/Shading Debug/Lighting"; // ToolbarOverlay 引用的唯一 ID。

        public BurtShadingDebugLightingDropdown() // Unity 通过无参构造创建 ToolbarElement。
            : base(BurtShadingDebugGroups.Lighting) // 绑定 Lighting 分类。
        {
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))] // 注册追加光分类按钮。
    internal sealed class BurtShadingDebugAdditionalLightingDropdown : BurtShadingDebugGroupDropdown
    {
        public const string Id = "BurtRP/Shading Debug/AdditionalLighting"; // ToolbarOverlay 引用的唯一 ID。

        public BurtShadingDebugAdditionalLightingDropdown() // Unity 通过无参构造创建 ToolbarElement。
            : base(BurtShadingDebugGroups.LightingAdditional) // 绑定追加光和 tiled light 分类。
        {
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))] // 注册间接光分类按钮。
    internal sealed class BurtShadingDebugIndirectLightingDropdown : BurtShadingDebugGroupDropdown
    {
        public const string Id = "BurtRP/Shading Debug/IndirectLighting"; // ToolbarOverlay 引用的唯一 ID。

        public BurtShadingDebugIndirectLightingDropdown() // Unity 通过无参构造创建 ToolbarElement。
            : base(BurtShadingDebugGroups.LightingIndirect) // 绑定间接光和最终输出分类。
        {
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))] // 注册 Shadow 分类按钮。
    internal sealed class BurtShadingDebugShadowDropdown : BurtShadingDebugGroupDropdown
    {
        public const string Id = "BurtRP/Shading Debug/Shadow"; // ToolbarOverlay 引用的唯一 ID。

        public BurtShadingDebugShadowDropdown() // Unity 通过无参构造创建 ToolbarElement。
            : base(BurtShadingDebugGroups.Shadow) // 绑定 Shadow 独立分类。
        {
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))] // 注册 Fullscreen 分类按钮。
    internal sealed class BurtShadingDebugFullscreenDropdown : BurtShadingDebugGroupDropdown
    {
        public const string Id = "BurtRP/Shading Debug/Fullscreen"; // ToolbarOverlay 引用的唯一 ID。

        public BurtShadingDebugFullscreenDropdown() // Unity 通过无参构造创建 ToolbarElement。
            : base(BurtShadingDebugGroups.Fullscreen) // 绑定 Fullscreen / Render Data 分类。
        {
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))] // 注册 SSAO 后处理分类按钮。
    internal sealed class BurtShadingDebugSSAODropdown : BurtShadingDebugGroupDropdown
    {
        public const string Id = "BurtRP/Shading Debug/SSAO"; // ToolbarOverlay 引用的唯一 ID。

        public BurtShadingDebugSSAODropdown() // Unity 通过无参构造创建 ToolbarElement。
            : base(BurtShadingDebugGroups.ScreenSpaceAmbientOcclusion) // 绑定 SSAO 后处理调试分类。
        {
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))]
    internal sealed class BurtShadingDebugScreenSpaceShadowDropdown : BurtShadingDebugGroupDropdown
    {
        public const string Id = "BurtRP/Shading Debug/SSShadow";

        public BurtShadingDebugScreenSpaceShadowDropdown()
            : base(BurtShadingDebugGroups.ScreenSpaceShadow)
        {
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))]
    internal sealed class BurtShadingDebugBurtGIDropdown : BurtShadingDebugGroupDropdown
    {
        public const string Id = "BurtRP/Shading Debug/BurtGI";

        public BurtShadingDebugBurtGIDropdown()
            : base(BurtShadingDebugGroups.ScreenSpaceGlobalIllumination)
        {
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))]
    internal sealed class BurtShadingDebugSSSDropdown : BurtShadingDebugGroupDropdown
    {
        public const string Id = "BurtRP/Shading Debug/ScreenSpaceSubsurface";

        public BurtShadingDebugSSSDropdown()
            : base(BurtShadingDebugGroups.ScreenSpaceSubsurface)
        {
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))]
    internal sealed class BurtShadingDebugFurBlurDropdown : BurtShadingDebugGroupDropdown
    {
        public const string Id = "BurtRP/Shading Debug/FurBlur";

        public BurtShadingDebugFurBlurDropdown()
            : base(BurtShadingDebugGroups.FurBlur)
        {
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))] // 注册 Bloom 后处理分类按钮。
    internal sealed class BurtShadingDebugBloomDropdown : BurtShadingDebugGroupDropdown
    {
        public const string Id = "BurtRP/Shading Debug/Bloom"; // ToolbarOverlay 引用的唯一 ID。

        public BurtShadingDebugBloomDropdown() // Unity 通过无参构造创建 ToolbarElement。
            : base(BurtShadingDebugGroups.Bloom) // 绑定 Bloom 后处理调试分类。
        {
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))] // 注册自动曝光后处理分类按钮。
    internal sealed class BurtShadingDebugAutoExposureDropdown : BurtShadingDebugGroupDropdown
    {
        public const string Id = "BurtRP/Shading Debug/AutoExposure"; // ToolbarOverlay 引用的唯一 ID。

        public BurtShadingDebugAutoExposureDropdown() // Unity 通过无参构造创建 ToolbarElement。
            : base(BurtShadingDebugGroups.AutoExposure) // 绑定自动曝光后处理调试分类。
        {
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))] // 注册 SSR 后处理分类按钮。
    internal sealed class BurtShadingDebugSSRDropdown : BurtShadingDebugGroupDropdown
    {
        public const string Id = "BurtRP/Shading Debug/SSR"; // ToolbarOverlay 引用的唯一 ID。

        public BurtShadingDebugSSRDropdown() // Unity 通过无参构造创建 ToolbarElement。
            : base(BurtShadingDebugGroups.ScreenSpaceReflections) // 绑定 SSR 后处理调试分类。
        {
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))] // 注册 TAA 后处理分类按钮。
    internal sealed class BurtShadingDebugTemporalAADropdown : BurtShadingDebugGroupDropdown
    {
        public const string Id = "BurtRP/Shading Debug/TAA"; // ToolbarOverlay 引用的唯一 ID。

        public BurtShadingDebugTemporalAADropdown() // Unity 通过无参构造创建 ToolbarElement。
            : base(BurtShadingDebugGroups.TemporalAA) // 绑定 TAA 后处理调试分类。
        {
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))]
    internal sealed class BurtShadingDebugAtmosphereDropdown : BurtShadingDebugGroupDropdown
    {
        public const string Id = "BurtRP/Shading Debug/Atmosphere";

        public BurtShadingDebugAtmosphereDropdown()
            : base(BurtShadingDebugGroups.Atmosphere)
        {
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))]
    internal sealed class BurtShadingDebugFogDropdown : BurtShadingDebugGroupDropdown
    {
        public const string Id = "BurtRP/Shading Debug/Fog";

        public BurtShadingDebugFogDropdown()
            : base(BurtShadingDebugGroups.Fog)
        {
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))]
    internal sealed class BurtShadingDebugVolumetricFogDropdown : BurtShadingDebugGroupDropdown
    {
        public const string Id = "BurtRP/Shading Debug/VolumetricFog";

        public BurtShadingDebugVolumetricFogDropdown()
            : base(BurtShadingDebugGroups.VolumetricFog)
        {
        }
    }

    internal sealed class BurtShadingDebugPopup : PopupWindowContent // 每个分类按钮点击后弹出的菜单内容。
    {
        private const float ScrollMaxHeight = 320f; // 单个分类仍限制最大高度，后续模式增加时不会撑出屏幕。
        private const float AtmosphereReadoutHeight = 350f;
        private const float AutoExposureReadoutHeight = 184f;
        private const float FogReadoutHeight = 210f;
        private const float VolumetricFogReadoutHeight = 254f;

        private readonly BurtShadingDebugGroup group; // 当前弹窗展示的分类。

        private Vector2 scrollPosition; // 保存滚动位置，长分类可滚动浏览。

        public BurtShadingDebugPopup(BurtShadingDebugGroup group) // 弹窗构造函数。
        {
            this.group = group; // 保存分类数据供绘制使用。
        }

        public override Vector2 GetWindowSize() // 返回 Popup 尺寸。
        {
            float listHeight = group.VisibleModeCount * EditorGUIUtility.singleLineHeight + 8f; // 根据模式数量估算列表高度。
            float contentHeight = 30f + Mathf.Min(listHeight, ScrollMaxHeight) + 48f; // 预留标题和说明区域，不再显示资产信息行。
            if (IsAutoExposureGroup())
            {
                contentHeight += AutoExposureReadoutHeight;
            }

            if (IsAtmosphereGroup())
            {
                contentHeight += AtmosphereReadoutHeight;
            }

            if (IsFogGroup())
            {
                contentHeight += FogReadoutHeight;
            }

            if (IsVolumetricFogGroup())
            {
                contentHeight += VolumetricFogReadoutHeight;
            }

            return new Vector2(320f, contentHeight); // 固定宽度，避免不同分类宽度跳变太大。
        }

        public override void OnGUI(Rect rect) // 绘制分类菜单。
        {
            EditorGUILayout.LabelField(group.Title, EditorStyles.boldLabel); // 显示分类标题。

            float listHeight = Mathf.Min(ScrollMaxHeight, group.VisibleModeCount * EditorGUIUtility.singleLineHeight + 8f); // 限制滚动区域高度。
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(listHeight)); // 开始绘制可滚动模式列表。

            foreach (var mode in group.Modes) // 遍历当前分类下的所有模式。
            {
                if (!BurtShadingDebugSettings.IsImportantMode(mode))
                {
                    continue;
                }

                DrawMode(mode); // 绘制单个模式项。
            }

            EditorGUILayout.EndScrollView(); // 结束滚动区域。
            EditorGUILayout.Space(4f); // 与资产信息隔开一点距离。

            if (IsAutoExposureGroup())
            {
                DrawAutoExposureReadout();
                EditorGUILayout.Space(4f);
            }

            if (IsAtmosphereGroup())
            {
                DrawAtmosphereReadout();
                EditorGUILayout.Space(4f);
            }

            if (IsFogGroup())
            {
                DrawFogReadout();
                EditorGUILayout.Space(4f);
            }

            if (IsVolumetricFogGroup())
            {
                DrawVolumetricFogReadout();
                EditorGUILayout.Space(4f);
            }

            EditorGUILayout.HelpBox("参考 XRender Shader Debug 的分类 Toolbar；Material、Lighting、Post Process 分别在独立 Overlay 中按细分栏位展开。", MessageType.Info); // 说明分类来源和全局 Debug 行为。
        }

        private void DrawMode(BurtShadingDebugMode mode) // 绘制一个可选 Debug 模式。
        {
            var isCurrent = BurtShadingDebugSettings.Mode == mode; // 判断该模式是否是当前模式。

            bool isSelectedAfterClick = GUILayout.Toggle(isCurrent, BurtShadingDebugDisplayNames.GetDisplayName(mode), "MenuItem"); // 使用 MenuItem 样式获得类似 Unity 菜单的勾选效果。
            if (isSelectedAfterClick == isCurrent) // 状态没有变化时说明这一项没有被点击。
            {
                return; // 未点击或点击当前项取消时不做任何改变。
            }

            BurtShadingDebugOverlayUtility.SetMode(isCurrent ? BurtShadingDebugMode.None : mode); // 再次点击当前 Debug 项时切回正常画面。
            editorWindow.Close(); // 选择后关闭弹窗，和常规 Dropdown 菜单行为一致。
        }

        private bool IsAutoExposureGroup()
        {
            return ReferenceEquals(group, BurtShadingDebugGroups.AutoExposure);
        }

        private bool IsAtmosphereGroup()
        {
            return ReferenceEquals(group, BurtShadingDebugGroups.Atmosphere);
        }

        private bool IsFogGroup()
        {
            return ReferenceEquals(group, BurtShadingDebugGroups.Fog);
        }

        private bool IsVolumetricFogGroup()
        {
            return ReferenceEquals(group, BurtShadingDebugGroups.VolumetricFog);
        }

        private static void DrawAtmosphereReadout()
        {
            var snapshot = BurtAtmosphereDebugUtility.GetSnapshot();
            EditorGUILayout.LabelField("Atmosphere State", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Enabled", snapshot.Enabled.ToString());
            EditorGUILayout.LabelField("Scattering", "Rayleigh " + Format(snapshot.RayleighIntensity) + "   Mie " + Format(snapshot.MieIntensity) + "   g " + Format(snapshot.MieAnisotropy));
            EditorGUILayout.LabelField("Scale Height", "Rayleigh " + Format(snapshot.RayleighScaleHeight) + "km   Mie " + Format(snapshot.MieScaleHeight) + "km");
            EditorGUILayout.LabelField("Planet", "Radius " + Format(snapshot.PlanetRadius) + "km   Atmosphere " + Format(snapshot.AtmosphereHeight) + "km");
            EditorGUILayout.LabelField("Sun", snapshot.SunSource + "   Intensity " + Format(snapshot.SunIntensity) + "   Clamp " + Format(snapshot.TonemapSafeSunIntensity));
            EditorGUILayout.LabelField("Sun Disk", "Size " + Format(snapshot.SunDiskSize) + "   Intensity " + Format(snapshot.SunDiskIntensity));
            EditorGUILayout.LabelField("Sun Halo", "Size " + Format(snapshot.SunHaloSize) + "   Intensity " + Format(snapshot.SunHaloIntensity));
            EditorGUILayout.LabelField("Horizon", "Intensity " + Format(snapshot.HorizonIntensity) + "   Falloff " + Format(snapshot.HorizonFalloff) + "   Sunset " + Format(snapshot.HorizonSunsetInfluence));
            EditorGUILayout.LabelField("Horizon Color", FormatColor(snapshot.HorizonColor));
            EditorGUILayout.LabelField("Sunset Color", FormatColor(snapshot.HorizonSunsetColor));
            EditorGUILayout.LabelField("Ground", Format(snapshot.GroundContribution) + "   Blend " + Format(snapshot.GroundBlendStart) + " / " + Format(snapshot.GroundBlendEnd));
            EditorGUILayout.LabelField("Sky Tint", FormatColor(snapshot.SkyTint));
            EditorGUILayout.LabelField("Exposure EV", Format(snapshot.ExposureCompensation));
            EditorGUILayout.LabelField("Aerial", snapshot.AerialPerspectiveEnabled + "   " + snapshot.AerialPerspectivePlacement + "   " + snapshot.FogInteraction);
            EditorGUILayout.LabelField("Aerial Shape", "Intensity " + Format(snapshot.AerialPerspectiveIntensity) + "   Distance " + Format(snapshot.AerialPerspectiveDistance));
            EditorGUILayout.LabelField("Aerial Fade", Format(snapshot.AerialPerspectiveNearFadeStart) + " / " + Format(snapshot.AerialPerspectiveNearFadeEnd) + "   Max " + Format(snapshot.AerialPerspectiveMaxOpacity));
            EditorGUILayout.LabelField("Formula", snapshot.SkyFormula + " / " + snapshot.AerialFormula);

            if (GUILayout.Button("Copy Atmosphere Readout"))
            {
                GUIUtility.systemCopyBuffer = FormatAtmosphereReadout(snapshot);
            }
        }

        private static string FormatAtmosphereReadout(BurtAtmosphereDebugSnapshot snapshot)
        {
            return
                "Burt Atmosphere Readout\n" +
                "Enabled: " + snapshot.Enabled + "\n" +
                "Scattering: rayleigh=" + Format(snapshot.RayleighIntensity) + " mie=" + Format(snapshot.MieIntensity) + " g=" + Format(snapshot.MieAnisotropy) + "\n" +
                "ScaleHeightKm: rayleigh=" + Format(snapshot.RayleighScaleHeight) + " mie=" + Format(snapshot.MieScaleHeight) + "\n" +
                "PlanetKm: radius=" + Format(snapshot.PlanetRadius) + " atmosphereHeight=" + Format(snapshot.AtmosphereHeight) + "\n" +
                "Sun: source=" + snapshot.SunSource + " intensity=" + Format(snapshot.SunIntensity) + " clamp=" + Format(snapshot.TonemapSafeSunIntensity) + " customDirection=" + FormatVector(snapshot.CustomSunDirection) + "\n" +
                "SunDisk: size=" + Format(snapshot.SunDiskSize) + " intensity=" + Format(snapshot.SunDiskIntensity) + "\n" +
                "SunHalo: size=" + Format(snapshot.SunHaloSize) + " intensity=" + Format(snapshot.SunHaloIntensity) + "\n" +
                "Art: horizonIntensity=" + Format(snapshot.HorizonIntensity) + " horizonFalloff=" + Format(snapshot.HorizonFalloff) + " horizonSunsetInfluence=" + Format(snapshot.HorizonSunsetInfluence) + " ground=" + Format(snapshot.GroundContribution) + " groundBlend=" + Format(snapshot.GroundBlendStart) + "/" + Format(snapshot.GroundBlendEnd) + " exposureEV=" + Format(snapshot.ExposureCompensation) + "\n" +
                "Tint: sky=" + FormatColor(snapshot.SkyTint) + " horizon=" + FormatColor(snapshot.HorizonColor) + " sunset=" + FormatColor(snapshot.HorizonSunsetColor) + " ground=" + FormatColor(snapshot.GroundColor) + " aerial=" + FormatColor(snapshot.AerialPerspectiveTint) + "\n" +
                "Aerial: enabled=" + snapshot.AerialPerspectiveEnabled + " intensity=" + Format(snapshot.AerialPerspectiveIntensity) + " distance=" + Format(snapshot.AerialPerspectiveDistance) + " heightFalloff=" + Format(snapshot.AerialPerspectiveHeightFalloff) + "\n" +
                "AerialFade: near=" + Format(snapshot.AerialPerspectiveNearFadeStart) + "/" + Format(snapshot.AerialPerspectiveNearFadeEnd) + " maxOpacity=" + Format(snapshot.AerialPerspectiveMaxOpacity) + "\n" +
                "AerialRouting: placement=" + snapshot.AerialPerspectivePlacement + " fogInteraction=" + snapshot.FogInteraction + "\n" +
                "Formula: sky=" + snapshot.SkyFormula + " aerial=" + snapshot.AerialFormula;
        }

        private static void DrawFogReadout()
        {
            var snapshot = BurtFogDebugUtility.GetSnapshot();
            EditorGUILayout.LabelField("Fog State", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Enabled", snapshot.Enabled.ToString());
            EditorGUILayout.LabelField("Shape", "Height " + Format(snapshot.Height) + "   Density " + Format(snapshot.Density));
            EditorGUILayout.LabelField("Falloff", Format(snapshot.HeightFalloff));
            EditorGUILayout.LabelField("Distance", "Start " + Format(snapshot.StartDistance) + "   Cutoff " + Format(snapshot.CutoffDistance));
            EditorGUILayout.LabelField("Max Opacity", Format(snapshot.MaxOpacity));
            EditorGUILayout.LabelField("Albedo", FormatColor(snapshot.Albedo));
            EditorGUILayout.LabelField("Lighting", "Directional " + Format(snapshot.DirectionalIntensity) + "   Ambient " + Format(snapshot.AmbientIntensity));
            EditorGUILayout.LabelField("Anisotropy", Format(snapshot.Anisotropy));
            EditorGUILayout.LabelField("Formula", snapshot.Formula);

            if (GUILayout.Button("Copy Fog Readout"))
            {
                GUIUtility.systemCopyBuffer = FormatFogReadout(snapshot);
            }
        }

        private static string FormatFogReadout(BurtFogDebugSnapshot snapshot)
        {
            return
                "Burt Fog Readout\n" +
                "Enabled: " + snapshot.Enabled + "\n" +
                "Height: " + Format(snapshot.Height) + "\n" +
                "Density: " + Format(snapshot.Density) + "\n" +
                "Falloff: " + Format(snapshot.HeightFalloff) + "\n" +
                "Distance: start=" + Format(snapshot.StartDistance) + " cutoff=" + Format(snapshot.CutoffDistance) + "\n" +
                "MaxOpacity: " + Format(snapshot.MaxOpacity) + "\n" +
                "Albedo: " + FormatColor(snapshot.Albedo) + "\n" +
                "Scattering: directional=" + Format(snapshot.DirectionalIntensity) + " ambient=" + Format(snapshot.AmbientIntensity) + " anisotropy=" + Format(snapshot.Anisotropy) + "\n" +
                "Formula: " + snapshot.Formula;
        }

        private static void DrawVolumetricFogReadout()
        {
            var snapshot = BurtVolumetricFogDebugUtility.GetSnapshot();
            EditorGUILayout.LabelField("Volumetric Fog State", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Enabled", snapshot.Enabled.ToString());
            EditorGUILayout.LabelField("Range", "Visible " + Format(snapshot.VisibleDistance) + "   Start " + Format(snapshot.StartDistance));
            EditorGUILayout.LabelField("Marching", "Steps " + snapshot.StepCount + "   Jitter " + snapshot.Jitter);
            EditorGUILayout.LabelField("Shape", "Height " + Format(snapshot.Height) + "   Density " + Format(snapshot.Density));
            EditorGUILayout.LabelField("Falloff", Format(snapshot.HeightFalloff));
            EditorGUILayout.LabelField("Extinction", Format(snapshot.ExtinctionScale));
            EditorGUILayout.LabelField("Max Opacity", Format(snapshot.MaxOpacity));
            EditorGUILayout.LabelField("Albedo", FormatColor(snapshot.Albedo));
            EditorGUILayout.LabelField("Phase", "Anisotropy " + Format(snapshot.Anisotropy));
            EditorGUILayout.LabelField("Lighting", "Direct " + Format(snapshot.DirectIntensity) + "   Ambient " + Format(snapshot.AmbientIntensity));
            EditorGUILayout.LabelField("Formula", snapshot.Formula);

            if (GUILayout.Button("Copy Volumetric Fog Readout"))
            {
                GUIUtility.systemCopyBuffer = FormatVolumetricFogReadout(snapshot);
            }
        }

        private static string FormatVolumetricFogReadout(BurtVolumetricFogDebugSnapshot snapshot)
        {
            return
                "Burt Volumetric Fog Readout\n" +
                "Enabled: " + snapshot.Enabled + "\n" +
                "VisibleDistance: " + Format(snapshot.VisibleDistance) + "\n" +
                "StartDistance: " + Format(snapshot.StartDistance) + "\n" +
                "StepCount: " + snapshot.StepCount + "\n" +
                "Jitter: " + snapshot.Jitter + "\n" +
                "Height: " + Format(snapshot.Height) + "\n" +
                "Density: " + Format(snapshot.Density) + "\n" +
                "Falloff: " + Format(snapshot.HeightFalloff) + "\n" +
                "ExtinctionScale: " + Format(snapshot.ExtinctionScale) + "\n" +
                "MaxOpacity: " + Format(snapshot.MaxOpacity) + "\n" +
                "Albedo: " + FormatColor(snapshot.Albedo) + "\n" +
                "Scattering: direct=" + Format(snapshot.DirectIntensity) + " ambient=" + Format(snapshot.AmbientIntensity) + " anisotropy=" + Format(snapshot.Anisotropy) + "\n" +
                "Formula: " + snapshot.Formula;
        }

        private static void DrawAutoExposureReadout()
        {
            var sceneView = SceneView.lastActiveSceneView;
            var camera = sceneView != null ? sceneView.camera : null;
            if (camera == null)
            {
                EditorGUILayout.HelpBox("Auto Exposure: no active SceneView camera.", MessageType.Info);
                return;
            }

            if (!BurtAutoExposureDebugUtility.TryGetSnapshot(camera, out var snapshot))
            {
                EditorGUILayout.HelpBox("Auto Exposure: no per-camera state yet. Select an automatic exposure mode and render one frame.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Auto Exposure State", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Camera", camera.name);
            EditorGUILayout.LabelField("Mode", snapshot.Mode.ToString());
            EditorGUILayout.LabelField("EV100", Format(snapshot.CurrentEV100) + " -> " + Format(snapshot.TargetEV100) + "   Range " + Format(snapshot.MinEV100) + " / " + Format(snapshot.MaxEV100));
            EditorGUILayout.LabelField("Average", "Luma " + Format(snapshot.AverageLuminance) + "   Log2 " + Format(snapshot.AverageLogLuminance));
            EditorGUILayout.LabelField("Status", snapshot.SampleStatus + (snapshot.UsingFallbackSample ? " (fallback)" : string.Empty));
            EditorGUILayout.LabelField("Sample", "Has=" + snapshot.HasSample + " Count=" + snapshot.SampleCount + " Age=" + FormatFrameAge(snapshot.SampleAgeFrames));
            EditorGUILayout.LabelField("Readback", "Pending=" + snapshot.ReadbackPending + " RequestAge=" + FormatFrameAge(snapshot.ReadbackAgeFrames) + " DoneAge=" + FormatFrameAge(snapshot.ReadbackCompletedAgeFrames));
            EditorGUILayout.LabelField("Readback Size", FormatReadbackSize(snapshot));
            EditorGUILayout.LabelField("Rejected", snapshot.SampleRejectedReason);

            if (GUILayout.Button("Copy Readout"))
            {
                GUIUtility.systemCopyBuffer = FormatAutoExposureReadout(camera, snapshot);
            }
        }

        private static string FormatAutoExposureReadout(Camera camera, BurtAutoExposureDebugSnapshot snapshot)
        {
            return
                "Burt Auto Exposure Readout\n" +
                "Camera: " + (camera != null ? camera.name : "n/a") + "\n" +
                "Mode: " + snapshot.Mode + "\n" +
                "EV100: current=" + Format(snapshot.CurrentEV100) + " target=" + Format(snapshot.TargetEV100) + " range=" + Format(snapshot.MinEV100) + "/" + Format(snapshot.MaxEV100) + "\n" +
                "Average: luminance=" + Format(snapshot.AverageLuminance) + " log2=" + Format(snapshot.AverageLogLuminance) + "\n" +
                "Status: " + snapshot.SampleStatus + " fallback=" + snapshot.UsingFallbackSample + "\n" +
                "Sample: has=" + snapshot.HasSample + " count=" + snapshot.SampleCount + " age=" + FormatFrameAge(snapshot.SampleAgeFrames) + "\n" +
                "Readback: pending=" + snapshot.ReadbackPending + " requestAge=" + FormatFrameAge(snapshot.ReadbackAgeFrames) + " doneAge=" + FormatFrameAge(snapshot.ReadbackCompletedAgeFrames) + " size=" + FormatReadbackSize(snapshot) + "\n" +
                "Rejected: " + snapshot.SampleRejectedReason + "\n" +
                "Metering: middleGrey=" + Format(snapshot.MiddleGrey) + " percentile=" + Format(snapshot.LowPercent) + "/" + Format(snapshot.HighPercent) + " histogramEV100=" + Format(snapshot.HistogramMinEV100) + "/" + Format(snapshot.HistogramMaxEV100);
        }

        private static string Format(float value)
        {
            return value.ToString("0.###");
        }

        private static string FormatColor(Color color)
        {
            return "(" + Format(color.r) + ", " + Format(color.g) + ", " + Format(color.b) + ")";
        }

        private static string FormatVector(Vector3 value)
        {
            return "(" + Format(value.x) + ", " + Format(value.y) + ", " + Format(value.z) + ")";
        }

        private static string FormatFrameAge(int age)
        {
            return age >= 0 ? age.ToString() : "n/a";
        }

        private static string FormatReadbackSize(BurtAutoExposureDebugSnapshot snapshot)
        {
            return snapshot.ReadbackWidth > 0 && snapshot.ReadbackHeight > 0
                ? snapshot.ReadbackWidth + "x" + snapshot.ReadbackHeight
                : "n/a";
        }
    }

    internal static class BurtShadingDebugOverlayUtility // Overlay 和 fallback window 共用的小工具。
    {
        public static void SyncExistingDebugViews(BurtShadingDebugMode mode) // 把 enum 模式同步到 BurtRP 既有全屏 Debug bool。
        {
            var asset = GetActiveBurtAsset(); // 获取当前生效的 BurtRP Asset。

            if (asset == null) // 非 BurtRP 或未绑定资产时无法同步。
            {
                return; // 直接返回，Shading Debug 的 shader 全局参数仍然有效。
            }

            var serializedAsset = new SerializedObject(asset); // 通过 SerializedObject 访问私有 SerializeField，避免改运行时 API。
            SetBool(serializedAsset, "enableDepthDebugView", mode == BurtShadingDebugMode.CameraDepth); // CameraDepth 模式开启现有深度调试。
            SetEnum(serializedAsset, "gBufferDebugViewMode", (int)ResolveGBufferDebugViewMode(mode)); // GBuffer 分类同步到资产上的全屏 GBuffer Debug 模式，让 RenderGraph 能插入 Burt Debug GBuffer。
            serializedAsset.ApplyModifiedPropertiesWithoutUndo(); // Debug 切换不写 Undo 栈，避免污染用户操作历史。
            EditorUtility.SetDirty(asset); // 标记资产已更新，Inspector 和渲染流程能看到变化。
        }

        public static void SetMode(BurtShadingDebugMode mode) // 设置 shading debug 模式并同步相关状态。
        {
            var normalizedMode = BurtShadingDebugSettings.NormalizeMode(mode);
            BurtShadingDebugSettings.Mode = normalizedMode; // 写入运行时静态状态，并上传 shader 全局参数。
            SyncExistingDebugViews(normalizedMode); // 同步 BurtRP Asset 上已有的 Depth / Shadow / GBuffer 全屏调试开关。
            BurtShadingDebugGroupDropdown.UpdateAllVisualStates(); // 刷新所有分类按钮的高亮和文本。
            SceneView.RepaintAll(); // 立即刷新 SceneView，避免等待下一次交互才看到结果。
        }

        public static BurtRenderPipelineAsset GetActiveBurtAsset() // 获取当前 Unity 设置中的 BurtRP Asset。
        {
            var asset = GraphicsSettings.currentRenderPipeline as BurtRenderPipelineAsset; // 优先读取 GraphicsSettings 当前管线。

            if (asset != null) // 如果项目级设置里已经是 BurtRP，直接返回。
            {
                return asset; // 返回当前 BurtRP Asset。
            }

            return QualitySettings.renderPipeline as BurtRenderPipelineAsset; // 否则尝试 QualitySettings 覆盖的渲染管线。
        }

        private static void SetBool(SerializedObject serializedObject, string propertyName, bool value) // 安全写入 bool SerializeField。
        {
            var property = serializedObject.FindProperty(propertyName); // 查找目标字段。

            if (property != null) // 字段存在才写入，兼容后续资产字段重命名或裁剪。
            {
                property.boolValue = value; // 设置 bool 值。
            }
        }

        private static void SetEnum(SerializedObject serializedObject, string propertyName, int value) // 安全写入 enum SerializeField。
        {
            var property = serializedObject.FindProperty(propertyName); // 查找目标字段。

            if (property != null) // 字段存在才写入，兼容后续资产字段重命名或裁剪。
            {
                property.intValue = value; // 写入枚举底层整数值，避免后续 enum 显式数值和 Inspector 索引不一致时同步错误。
            }
        }

        private static BurtGBufferDebugViewMode ResolveGBufferDebugViewMode(BurtShadingDebugMode mode) // 把 Overlay 的 GBuffer 分类映射到 BurtRenderPipelineAsset 的全屏 GBuffer Debug 模式。
        {
            switch (mode) // 逐项映射，避免非 GBuffer Debug 模式误触发全屏 GBuffer Pass。
            {
                case BurtShadingDebugMode.GBufferBaseColor: // Overlay 选择 GBuffer Base Color。
                    return BurtGBufferDebugViewMode.BaseColor; // 资产同步为 BaseColor。
                case BurtShadingDebugMode.GBufferNormalWS: // Overlay 选择 GBuffer Direction WS。
                    return BurtGBufferDebugViewMode.NormalWS; // 资产同步为 NormalWS。
                case BurtShadingDebugMode.GBufferMetallic: // Overlay 选择 GBuffer Material Channel。
                    return BurtGBufferDebugViewMode.Metallic; // 资产同步为 Metallic。
                case BurtShadingDebugMode.GBufferSmoothness: // Overlay 选择 GBuffer Smoothness。
                    return BurtGBufferDebugViewMode.Smoothness; // 资产同步为 Smoothness。
                case BurtShadingDebugMode.GBufferOcclusion: // Overlay 选择 GBuffer Occlusion。
                    return BurtGBufferDebugViewMode.Occlusion; // 资产同步为 Occlusion。
                case BurtShadingDebugMode.GBufferReflectance: // Overlay 选择 GBuffer Reflectance。
                    return BurtGBufferDebugViewMode.Reflectance; // 资产同步为 Reflectance。
                case BurtShadingDebugMode.GBufferRoughness: // Overlay 选择 GBuffer Roughness。
                    return BurtGBufferDebugViewMode.Roughness; // 资产同步为 Roughness。
                case BurtShadingDebugMode.GBufferDiffuseColor: // Overlay 选择 GBuffer Diffuse Color。
                    return BurtGBufferDebugViewMode.DiffuseColor; // 资产同步为 DiffuseColor。
                case BurtShadingDebugMode.GBufferHairStrandDirection: // Overlay 选择 Hair strand direction。
                    return BurtGBufferDebugViewMode.HairStrandDirection; // 资产同步为 HairStrandDirection。
                case BurtShadingDebugMode.GBufferHairScatter: // Overlay 选择 Hair scatter。
                    return BurtGBufferDebugViewMode.HairScatter; // 资产同步为 HairScatter。
                case BurtShadingDebugMode.GBufferHairShift: // Overlay 选择 Hair longitudinal shift scale。
                    return BurtGBufferDebugViewMode.HairShift; // 资产同步为 HairShift。
                case BurtShadingDebugMode.GBufferClearCoatMask:
                    return BurtGBufferDebugViewMode.ClearCoatMask;
                case BurtShadingDebugMode.GBufferClearCoatNormalWS:
                    return BurtGBufferDebugViewMode.ClearCoatNormalWS;
                case BurtShadingDebugMode.GBufferClearCoatRoughness:
                    return BurtGBufferDebugViewMode.ClearCoatRoughness;
                case BurtShadingDebugMode.GBufferSubsurfaceStrength:
                    return BurtGBufferDebugViewMode.SubsurfaceStrength;
                case BurtShadingDebugMode.GBufferSubsurfaceThickness:
                    return BurtGBufferDebugViewMode.SubsurfaceThickness;
                case BurtShadingDebugMode.GBufferSubsurfaceProfileIndex:
                    return BurtGBufferDebugViewMode.SubsurfaceProfileIndex;
                case BurtShadingDebugMode.GBufferFoliageTransmissionColor:
                    return BurtGBufferDebugViewMode.FoliageTransmissionColor;
                case BurtShadingDebugMode.GBufferFoliageTransmissionWeight:
                    return BurtGBufferDebugViewMode.FoliageTransmissionWeight;
                case BurtShadingDebugMode.GBufferFoliageThickness:
                    return BurtGBufferDebugViewMode.FoliageThickness;
                case BurtShadingDebugMode.GBufferFoliageTransmissionNdotL:
                    return BurtGBufferDebugViewMode.FoliageTransmissionNdotL;
                case BurtShadingDebugMode.GBufferFoliageSpecularScale:
                    return BurtGBufferDebugViewMode.FoliageSpecularScale;
                case BurtShadingDebugMode.GBufferFoliageScreenSpaceShadowIntensity:
                    return BurtGBufferDebugViewMode.FoliageScreenSpaceShadowIntensity;
                case BurtShadingDebugMode.GBufferAnisotropy:
                    return BurtGBufferDebugViewMode.Anisotropy;
                case BurtShadingDebugMode.GBufferTangentWS:
                    return BurtGBufferDebugViewMode.TangentWS;
                default: // 其他 Overlay 模式不应该显示真实 GBuffer。
                    return BurtGBufferDebugViewMode.Disabled; // 资产同步为 Disabled，避免切换到 Lighting/Material 后 GBuffer Debug 残留。
            }
        }
    }

}
