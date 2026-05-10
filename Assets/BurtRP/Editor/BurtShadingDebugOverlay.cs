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
                case BurtShadingDebugMode.GBufferBaseColor:
                    return "GBuffer Base Color"; // 显示 GBuffer0.rgb 解码后的 BaseColor。
                case BurtShadingDebugMode.GBufferNormalWS:
                    return "GBuffer Normal WS"; // 显示 oct normal 解码后的世界空间法线。
                case BurtShadingDebugMode.GBufferMetallic:
                    return "GBuffer Metallic"; // 显示 GBuffer1.b 解码后的 Metallic。
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
                case BurtShadingDebugMode.DetailLighting:
                    return "Detail Lighting"; // 对齐 XRender DEBUGID_LIGHTING_DETAIL_LIGHTING。
                case BurtShadingDebugMode.IndirectLighting:
                    return "Indirect Lighting Total"; // 显示 SH diffuse + reflection probe specular。
                case BurtShadingDebugMode.DirectDiffuse:
                    return "Direct Diffuse"; // 显示直接漫反射最终贡献。
                case BurtShadingDebugMode.DirectSpecular:
                    return "Direct Specular"; // 显示直接高光最终贡献。
                case BurtShadingDebugMode.IndirectDiffuse:
                    return "Indirect Diffuse"; // 显示 SH / Light Probe 漫反射。
                case BurtShadingDebugMode.IndirectSpecular:
                    return "Indirect Specular"; // 显示 Reflection Probe / Sky 高光。
                case BurtShadingDebugMode.ShadowAttenuation:
                    return "Shadow Attenuation"; // 显示主光阴影衰减，白色表示不在阴影中。
                case BurtShadingDebugMode.AmbientOcclusion:
                    return "Ambient Occlusion (Lighting)"; // 显示真正参与间接光遮蔽的 AO。
                case BurtShadingDebugMode.Emission:
                    return "Emission"; // 显示自发光贡献。
                case BurtShadingDebugMode.FinalLighting:
                    return "Final Lighting"; // 显示写入 CameraColor 前的最终材质颜色。
                case BurtShadingDebugMode.CameraDepth:
                    return "Camera Depth"; // 已有全屏深度调试。
                case BurtShadingDebugMode.MainLightShadow:
                    return "Main Light Shadow"; // 已有主光阴影图调试。
                case BurtShadingDebugMode.ScreenSpaceReflectionHitMask:
                    return "SSR Hit Mask"; // 显示 SSR raymarch 是否命中。
                case BurtShadingDebugMode.ScreenSpaceReflectionHitUV:
                    return "SSR Hit UV"; // 显示 SSR 命中点屏幕 UV。
                case BurtShadingDebugMode.ScreenSpaceReflectionStepCount:
                    return "SSR Step Count"; // 显示 SSR raymarch 步数。
                case BurtShadingDebugMode.ScreenSpaceReflectionColor:
                    return "SSR Reflection Color"; // 显示 SSR 命中后采样到的反射颜色。
                case BurtShadingDebugMode.TemporalAAHistory:
                    return "TAA History Color"; // 显示 TAA 重投影采样到的 history。
                case BurtShadingDebugMode.TemporalAAFeedback:
                    return "TAA Feedback"; // 显示最终 history 混合权重。
                case BurtShadingDebugMode.TemporalAARejection:
                    return "TAA Rejection"; // 显示 luma / clip / depth 拒绝分量。
                case BurtShadingDebugMode.TemporalAAHistoryUV:
                    return "TAA History UV"; // 显示重投影后的 history UV 和屏幕内状态。
                case BurtShadingDebugMode.TemporalAADifference:
                    return "TAA Difference"; // 显示当前帧与 history 的颜色差异。
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

        public bool Contains(BurtShadingDebugMode mode) // 判断当前模式是否属于这个分类，用来高亮对应 Dropdown。
        {
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

        public static readonly BurtShadingDebugGroup Material = new BurtShadingDebugGroup("Material / Generic Data", "Material", new[] // 材质输入和 XRender GenericData 派生值。
        {
            BurtShadingDebugMode.Albedo, // BaseMap 与 BaseColor 合成后的基础色。
            BurtShadingDebugMode.DiffuseColor, // metallic 扣除后的 diffuse 颜色。
            BurtShadingDebugMode.NormalWS, // 最终世界空间法线。
            BurtShadingDebugMode.Smoothness, // 面板语义下的光滑度。
            BurtShadingDebugMode.Roughness, // shader 内部使用的感知粗糙度。
            BurtShadingDebugMode.Metallic, // 最终金属度。
            BurtShadingDebugMode.Occlusion, // 最终 AO。
            BurtShadingDebugMode.Reflectance // XRender reflectance 输入。
        });

        public static readonly BurtShadingDebugGroup GBuffer = new BurtShadingDebugGroup("GBuffer / Deferred Data", "GBuffer", new[] // Deferred 前置检查：验证 GBuffer 编解码和 PBRData 重建。
        {
            BurtShadingDebugMode.GBufferBaseColor, // GBuffer0.rgb 解码结果。
            BurtShadingDebugMode.GBufferNormalWS, // GBuffer1.rg oct normal 解码结果。
            BurtShadingDebugMode.GBufferMetallic, // GBuffer1.b 解码结果。
            BurtShadingDebugMode.GBufferSmoothness, // GBuffer1.a 解码结果。
            BurtShadingDebugMode.GBufferOcclusion, // GBuffer0.a 解码结果。
            BurtShadingDebugMode.GBufferReflectance, // GBuffer2.a 解码结果。
            BurtShadingDebugMode.GBufferRoughness, // GBuffer -> PBRMaterialData 的 roughness。
            BurtShadingDebugMode.GBufferDiffuseColor // GBuffer -> PBRMaterialData 的 DiffuseColor。
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

        public static readonly BurtShadingDebugGroup IBL = new BurtShadingDebugGroup("IBL / Energy / Occlusion", "IBL", new[] // 归档环境光、能量守恒和 specular occlusion 相关调试。
        {
            BurtShadingDebugMode.IndirectSpecularDFG, // PreIntegratedFG DFG.xy。
            BurtShadingDebugMode.IndirectSpecularEnvBRDF, // DFG 应用到 F0/F90 后的环境 BRDF。
            BurtShadingDebugMode.SpecularEnergyCompensation, // 直接高光补能。
            BurtShadingDebugMode.IndirectSpecularEnergyCompensation, // 间接高光补能。
            BurtShadingDebugMode.EnergyPreservation, // diffuse 底层保能比例。
            BurtShadingDebugMode.SpecularOcclusion // 环境高光遮蔽。
        });

        public static readonly BurtShadingDebugGroup Lighting = new BurtShadingDebugGroup("Lighting", "Lighting", new[] // 对应 XRender Lighting Debug，保留 Detail Lighting 入口。
        {
            BurtShadingDebugMode.DetailLighting, // 中灰 BaseColor 下重新观察光照细节。
            BurtShadingDebugMode.DirectDiffuse, // 直接漫反射最终贡献。
            BurtShadingDebugMode.DirectSpecular, // 直接高光最终贡献。
            BurtShadingDebugMode.IndirectLighting, // 间接光总和。
            BurtShadingDebugMode.IndirectDiffuse, // 间接漫反射。
            BurtShadingDebugMode.IndirectSpecular, // 间接高光。
            BurtShadingDebugMode.ShadowAttenuation, // 主光阴影衰减。
            BurtShadingDebugMode.AmbientOcclusion, // 参与 lighting 的 AO。
            BurtShadingDebugMode.Emission, // 自发光贡献。
            BurtShadingDebugMode.FinalLighting // PBR 光照加自发光后的最终材质颜色。
        });

        public static readonly BurtShadingDebugGroup Fullscreen = new BurtShadingDebugGroup("Fullscreen / Render Data", "Fullscreen", new[] // BurtRP 现有全屏调试入口。
        {
            BurtShadingDebugMode.CameraDepth, // CameraDepth 全屏 Debug。
            BurtShadingDebugMode.MainLightShadow, // MainLightShadow 全屏 Debug。
            BurtShadingDebugMode.ScreenSpaceReflectionHitMask, // SSR 命中遮罩。
            BurtShadingDebugMode.ScreenSpaceReflectionHitUV, // SSR 命中 UV。
            BurtShadingDebugMode.ScreenSpaceReflectionStepCount, // SSR raymarch 步数。
            BurtShadingDebugMode.ScreenSpaceReflectionColor // SSR 采样到的反射颜色。
        });

        public static readonly BurtShadingDebugGroup TemporalAA = new BurtShadingDebugGroup("Temporal AA", "TAA", new[] // TAA 独立分类，避免和通用 Fullscreen 调试混在一起。
        {
            BurtShadingDebugMode.TemporalAAHistory, // TAA 重投影 history 颜色。
            BurtShadingDebugMode.TemporalAAFeedback, // TAA history feedback 权重。
            BurtShadingDebugMode.TemporalAARejection, // TAA 拒绝分量。
            BurtShadingDebugMode.TemporalAAHistoryUV, // TAA history UV。
            BurtShadingDebugMode.TemporalAADifference // TAA 当前帧与 history 差异。
        });
    }

    [Overlay(typeof(SceneView), "Burt Shading Debug")] // 在 SceneView 注册 BurtRP Shading Debug Overlay。
    internal sealed class BurtShadingDebugOverlay : ToolbarOverlay // 组合多个分类 Dropdown，参考 XRender XShaderDebugOverlay 的多按钮结构。
    {
        public BurtShadingDebugOverlay() // Unity 创建 Overlay 时会调用这个构造函数。
            : base(
                BurtShadingDebugOffDropdown.Id,
                BurtShadingDebugMaterialDropdown.Id,
                BurtShadingDebugGBufferDropdown.Id,
                BurtShadingDebugSpecularAADropdown.Id,
                BurtShadingDebugBRDFDropdown.Id,
                BurtShadingDebugIBLDropdown.Id,
                BurtShadingDebugLightingDropdown.Id,
                BurtShadingDebugFullscreenDropdown.Id,
                BurtShadingDebugTemporalAADropdown.Id) // 每个 ID 对应一个 EditorToolbarElement。
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
        }

        public void UpdateVisualState() // 根据全局 Debug 模式刷新按钮显示。
        {
            var mode = BurtShadingDebugSettings.Mode; // 读取当前选中的 Debug 模式。
            bool isActiveGroup = group.Contains(mode); // 当前模式属于该分类时高亮该 Dropdown，None 会高亮 Off。
            value = isActiveGroup; // 让 Toolbar Toggle 视觉上反映当前分类。
            text = isActiveGroup && mode != BurtShadingDebugMode.None ? BurtShadingDebugDisplayNames.GetDisplayName(mode) : group.ButtonText; // 激活分类显示具体模式，Off 保持短名。
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
    }

    [EditorToolbarElement(Id, typeof(SceneView))] // 注册 Off 分类按钮。
    internal sealed class BurtShadingDebugOffDropdown : BurtShadingDebugGroupDropdown
    {
        public const string Id = "BurtRP/Shading Debug/Off"; // ToolbarOverlay 引用的唯一 ID。

        public BurtShadingDebugOffDropdown() // Unity 通过无参构造创建 ToolbarElement。
            : base(BurtShadingDebugGroups.General) // 绑定 General / None 分类。
        {
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))] // 注册 Material 分类按钮。
    internal sealed class BurtShadingDebugMaterialDropdown : BurtShadingDebugGroupDropdown
    {
        public const string Id = "BurtRP/Shading Debug/Material"; // ToolbarOverlay 引用的唯一 ID。

        public BurtShadingDebugMaterialDropdown() // Unity 通过无参构造创建 ToolbarElement。
            : base(BurtShadingDebugGroups.Material) // 绑定 Material / Generic Data 分类。
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

    [EditorToolbarElement(Id, typeof(SceneView))] // 注册 Fullscreen 分类按钮。
    internal sealed class BurtShadingDebugFullscreenDropdown : BurtShadingDebugGroupDropdown
    {
        public const string Id = "BurtRP/Shading Debug/Fullscreen"; // ToolbarOverlay 引用的唯一 ID。

        public BurtShadingDebugFullscreenDropdown() // Unity 通过无参构造创建 ToolbarElement。
            : base(BurtShadingDebugGroups.Fullscreen) // 绑定 Fullscreen / Render Data 分类。
        {
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))] // 注册 TAA 分类按钮。
    internal sealed class BurtShadingDebugTemporalAADropdown : BurtShadingDebugGroupDropdown
    {
        public const string Id = "BurtRP/Shading Debug/TAA"; // ToolbarOverlay 引用的唯一 ID。

        public BurtShadingDebugTemporalAADropdown() // Unity 通过无参构造创建 ToolbarElement。
            : base(BurtShadingDebugGroups.TemporalAA) // 绑定 TAA 独立分类。
        {
        }
    }

    internal sealed class BurtShadingDebugPopup : PopupWindowContent // 每个分类按钮点击后弹出的菜单内容。
    {
        private const float ScrollMaxHeight = 320f; // 单个分类仍限制最大高度，后续模式增加时不会撑出屏幕。

        private readonly BurtShadingDebugGroup group; // 当前弹窗展示的分类。

        private Vector2 scrollPosition; // 保存滚动位置，长分类可滚动浏览。

        public BurtShadingDebugPopup(BurtShadingDebugGroup group) // 弹窗构造函数。
        {
            this.group = group; // 保存分类数据供绘制使用。
        }

        public override Vector2 GetWindowSize() // 返回 Popup 尺寸。
        {
            float listHeight = group.Modes.Length * EditorGUIUtility.singleLineHeight + 8f; // 根据模式数量估算列表高度。
            float contentHeight = 30f + Mathf.Min(listHeight, ScrollMaxHeight) + 48f; // 预留标题和说明区域，不再显示资产信息行。
            return new Vector2(320f, contentHeight); // 固定宽度，避免不同分类宽度跳变太大。
        }

        public override void OnGUI(Rect rect) // 绘制分类菜单。
        {
            EditorGUILayout.LabelField(group.Title, EditorStyles.boldLabel); // 显示分类标题。

            float listHeight = Mathf.Min(ScrollMaxHeight, group.Modes.Length * EditorGUIUtility.singleLineHeight + 8f); // 限制滚动区域高度。
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(listHeight)); // 开始绘制可滚动模式列表。

            foreach (var mode in group.Modes) // 遍历当前分类下的所有模式。
            {
                DrawMode(mode); // 绘制单个模式项。
            }

            EditorGUILayout.EndScrollView(); // 结束滚动区域。
            EditorGUILayout.Space(4f); // 与资产信息隔开一点距离。

            EditorGUILayout.HelpBox("参考 XRender Shader Debug 的分类 Toolbar；Depth / Shadow 同步现有全屏 Debug，SSR Debug 走 Global Volume 的 SSR 开关。", MessageType.Info); // 说明分类来源和全屏 Debug 行为。
        }

        private void DrawMode(BurtShadingDebugMode mode) // 绘制一个可选 Debug 模式。
        {
            var isCurrent = BurtShadingDebugSettings.Mode == mode; // 判断该模式是否是当前模式。

            if (!GUILayout.Toggle(isCurrent, BurtShadingDebugDisplayNames.GetDisplayName(mode), "MenuItem")) // 使用 MenuItem 样式获得类似 Unity 菜单的勾选效果。
            {
                return; // 未点击或点击当前项取消时不做任何改变。
            }

            if (!isCurrent) // 只在切换到新模式时更新状态。
            {
                SetMode(mode); // 写入全局 Debug 模式。
                editorWindow.Close(); // 选择后关闭弹窗，和常规 Dropdown 菜单行为一致。
            }
        }

        private void SetMode(BurtShadingDebugMode mode) // 设置 shading debug 模式并同步相关状态。
        {
            BurtShadingDebugSettings.Mode = mode; // 写入运行时静态状态，并上传 shader 全局参数。
            BurtShadingDebugOverlayUtility.SyncExistingDebugViews(mode); // 同步 BurtRP Asset 上已有的 Depth / Shadow 全屏调试开关。
            BurtShadingDebugGroupDropdown.UpdateAllVisualStates(); // 刷新所有分类按钮的高亮和文本。
            SceneView.RepaintAll(); // 立即刷新 SceneView，避免等待下一次交互才看到结果。
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
            SetBool(serializedAsset, "enableMainLightShadowDebugView", mode == BurtShadingDebugMode.MainLightShadow); // MainLightShadow 模式开启现有阴影调试。
            SetEnum(serializedAsset, "gBufferDebugViewMode", (int)ResolveGBufferDebugViewMode(mode)); // GBuffer 分类同步到资产上的全屏 GBuffer Debug 模式，让 RenderGraph 能插入 Burt Debug GBuffer。
            serializedAsset.ApplyModifiedPropertiesWithoutUndo(); // Debug 切换不写 Undo 栈，避免污染用户操作历史。
            EditorUtility.SetDirty(asset); // 标记资产已更新，Inspector 和渲染流程能看到变化。
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
                case BurtShadingDebugMode.GBufferNormalWS: // Overlay 选择 GBuffer Normal WS。
                    return BurtGBufferDebugViewMode.NormalWS; // 资产同步为 NormalWS。
                case BurtShadingDebugMode.GBufferMetallic: // Overlay 选择 GBuffer Metallic。
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
                default: // 其他 Overlay 模式不应该显示真实 GBuffer。
                    return BurtGBufferDebugViewMode.Disabled; // 资产同步为 Disabled，避免切换到 Lighting/Material 后 GBuffer Debug 残留。
            }
        }
    }

    internal sealed class BurtShadingDebugWindow : EditorWindow // Overlay 不可用时保留一个菜单窗口作为 fallback。
    {
        [MenuItem("Window/Rendering/BurtRP/Shading Debug")] // 提供 Window 菜单入口，方便没有打开 Overlay 的情况下切换。
        private static void Open() // 打开 fallback 窗口。
        {
            GetWindow<BurtShadingDebugWindow>("Burt Shading Debug"); // 创建或聚焦窗口。
        }

        private void OnGUI() // 绘制 fallback 窗口内容。
        {
            EditorGUILayout.LabelField("Overlay Fallback", EditorStyles.boldLabel); // 标题提示这是备用入口。
            EditorGUILayout.HelpBox("SceneView Overlay 会显示分类 Dropdown；这里保留完整 EnumPopup 作为备用入口。", MessageType.Info); // 说明推荐使用 Overlay。

            EditorGUI.BeginChangeCheck(); // 开始监听 enum 修改。
            var mode = (BurtShadingDebugMode)EditorGUILayout.EnumPopup("Mode", BurtShadingDebugSettings.Mode); // 备用入口仍显示完整 enum。

            if (EditorGUI.EndChangeCheck()) // 用户切换了模式。
            {
                BurtShadingDebugSettings.Mode = mode; // 写入全局 shader debug 状态。
                BurtShadingDebugOverlayUtility.SyncExistingDebugViews(mode); // 同步 Depth/Shadow 全屏 Debug 开关。
                BurtShadingDebugGroupDropdown.UpdateAllVisualStates(); // 刷新 Overlay 上的分类按钮。
                SceneView.RepaintAll(); // 刷新 SceneView。
            }

            EditorGUILayout.LabelField("Display", BurtShadingDebugDisplayNames.GetDisplayName(BurtShadingDebugSettings.Mode)); // 显示友好名，方便和 Overlay 对照。
            EditorGUILayout.LabelField("Shader Mode", BurtShadingDebugSettings.ModeShaderName); // 显示 shader mode 全局变量名。
            EditorGUILayout.LabelField("Shader Enabled", BurtShadingDebugSettings.EnabledShaderName); // 显示 shader enabled 全局变量名。
        }
    }
}
