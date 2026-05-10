using Burt.RenderPipeline; // 引入 BurtRP 运行时命名空间，Editor Overlay 需要读写 BurtShadingDebugSettings。
using UnityEditor; // 引入 UnityEditor，下面需要使用 EditorWindow、MenuItem、SerializedObject 等编辑器 API。
using UnityEditor.Overlays; // 引入 Overlay API，用来把 Shading Debug 挂到 SceneView Overlay。
using UnityEditor.Toolbars; // 引入 Toolbar API，用来创建 Overlay 上的下拉按钮。
using UnityEngine; // 引入 UnityEngine，下面需要使用 Vector2、Rect、ObjectNames 等 Unity 类型。
using UnityEngine.Rendering; // 引入渲染命名空间，下面需要读取 GraphicsSettings.currentRenderPipeline。
using UnityEngine.UIElements; // 引入 UIElements，EditorToolbarDropdownToggle 继承自 VisualElement。

namespace Burt.RenderPipeline.Editor // 使用 BurtRP Editor 命名空间，把编辑器扩展和运行时代码分开。
{
    internal static class BurtShadingDebugDisplayNames // 集中管理 Debug 菜单显示名，避免 Editor UI 直接暴露 enum 的工程命名。
    {
        public static string GetDisplayName(BurtShadingDebugMode mode) // 返回对用户更友好的显示名，参考 XRender DebugDefine.hlsl 的 [Display: xxx] 机制。
        {
            switch (mode) // 按模式逐项映射，方便后续新增 Debug 时明确给出菜单名。
            {
                case BurtShadingDebugMode.None:
                    return "None"; // 正常渲染，不启用任何 Shading Debug。
                case BurtShadingDebugMode.Albedo:
                    return "Base Color"; // 对齐 XRender Material Debug 里的 Base Color 命名。
                case BurtShadingDebugMode.NormalWS:
                    return "Normal World Space"; // 对齐 XRender 的 Normal World Space。
                case BurtShadingDebugMode.Smoothness:
                    return "Smoothness"; // BurtRP 面板仍使用 Smoothness，所以保留这个显示名。
                case BurtShadingDebugMode.Metallic:
                    return "Metallic"; // 金属度输入。
                case BurtShadingDebugMode.Occlusion:
                    return "Ambient Occlusion"; // 对齐 XRender GenericData.AmbientOcclusion 语义。
                case BurtShadingDebugMode.Reflectance:
                    return "Reflectance"; // XRender Specular.Reflectance 输入。
                case BurtShadingDebugMode.Roughness:
                    return "Roughness"; // 内部 XRender Base.Roughness 语义。
                case BurtShadingDebugMode.SpecularAARoughness:
                    return "Specular AA Roughness"; // 直接高光实际使用的过滤后 roughness。
                case BurtShadingDebugMode.SpecularEnergyCompensation:
                    return "Specular Energy Compensation"; // 直接高光能量补偿。
                case BurtShadingDebugMode.SpecularOcclusion:
                    return "Specular Occlusion"; // 间接高光遮蔽。
                case BurtShadingDebugMode.EnergyPreservation:
                    return "Energy Preservation"; // XRender EnergyPreservation。
                case BurtShadingDebugMode.IndirectSpecularEnergyCompensation:
                    return "Indirect Specular Energy Compensation"; // Reflection Probe / Sky Specular 补能。
                case BurtShadingDebugMode.DiffuseColor:
                    return "Diffuse Color"; // XRender GenericData.DiffuseColor。
                case BurtShadingDebugMode.F0:
                    return "F0"; // reflectance / metallic / baseColor 还原出的 F0。
                case BurtShadingDebugMode.F90:
                    return "F90"; // Schlick Fresnel 的掠射端点。
                case BurtShadingDebugMode.DirectBRDFD:
                    return "Direct BRDF D (GGX)"; // 直接光 GGX NDF D 项。
                case BurtShadingDebugMode.DirectBRDFVisibility:
                    return "Direct BRDF Visibility"; // 直接光 Smith Joint Visibility。
                case BurtShadingDebugMode.DirectBRDFFresnel:
                    return "Direct BRDF Fresnel"; // 直接光 Schlick Fresnel。
                case BurtShadingDebugMode.DirectDiffuseLobe:
                    return "Direct Diffuse Lobe"; // Lambert / Burley diffuse lobe。
                case BurtShadingDebugMode.DirectDiffuseBRDF:
                    return "Direct Diffuse BRDF"; // 未乘灯光可见性的 diffuse BRDF。
                case BurtShadingDebugMode.DirectSpecularBRDF:
                    return "Direct Specular BRDF"; // 未乘灯光可见性的 specular BRDF。
                case BurtShadingDebugMode.SpecularAANormalVariance:
                    return "Specular AA Normal Variance"; // XRender GeometricNormalVariance。
                case BurtShadingDebugMode.SpecularAARoughnessDelta:
                    return "Specular AA Roughness Delta"; // Specular AA 额外增加的 roughness。
                case BurtShadingDebugMode.IndirectSpecularDFG:
                    return "Indirect Specular DFG"; // PreIntegratedFG 的 DFG.xy。
                case BurtShadingDebugMode.IndirectSpecularEnvBRDF:
                    return "Indirect Specular Env BRDF"; // DFG 应用到 F0/F90 后的环境 BRDF。
                case BurtShadingDebugMode.GBufferBaseColor:
                    return "GBuffer Base Color"; // GBuffer0.rgb 解码后的基础色。
                case BurtShadingDebugMode.GBufferNormalWS:
                    return "GBuffer Normal WS"; // GBuffer1.rg octahedron normal 解码后的世界空间法线。
                case BurtShadingDebugMode.GBufferMetallic:
                    return "GBuffer Metallic"; // GBuffer1.b 解码后的金属度。
                case BurtShadingDebugMode.GBufferSmoothness:
                    return "GBuffer Smoothness"; // GBuffer1.a 解码后的光滑度。
                case BurtShadingDebugMode.GBufferOcclusion:
                    return "GBuffer Occlusion"; // GBuffer0.a 解码后的 AO。
                case BurtShadingDebugMode.GBufferReflectance:
                    return "GBuffer Reflectance"; // GBuffer2.a 解码后的 XRender reflectance。
                case BurtShadingDebugMode.DetailLighting:
                    return "Detail Lighting"; // 对齐 XRender DEBUGID_LIGHTING_DETAIL_LIGHTING 的 Display 名。
                case BurtShadingDebugMode.IndirectLighting:
                    return "Indirect Lighting Total"; // 对齐 XRender Indirect Lighting Total 语义。
                case BurtShadingDebugMode.DirectDiffuse:
                    return "Direct Diffuse"; // 直接漫反射最终贡献。
                case BurtShadingDebugMode.DirectSpecular:
                    return "Direct Specular"; // 直接高光最终贡献。
                case BurtShadingDebugMode.IndirectDiffuse:
                    return "Indirect Diffuse"; // SH / Light Probe 漫反射。
                case BurtShadingDebugMode.IndirectSpecular:
                    return "Indirect Specular"; // Reflection Probe / Sky Specular。
                case BurtShadingDebugMode.CameraDepth:
                    return "Camera Depth"; // 已有全屏深度调试。
                case BurtShadingDebugMode.MainLightShadow:
                    return "Main Light Shadow"; // 已有主光阴影图调试。
                default:
                    return ObjectNames.NicifyVariableName(mode.ToString()); // 兜底使用 Unity 的枚举名美化，避免新增模式显示为空。
            }
        }
    }

    [Overlay(typeof(SceneView), "Burt Shading Debug")] // 把这个 Overlay 注册到 SceneView，名字显示为 Burt Shading Debug。
    internal sealed class BurtShadingDebugOverlay : ToolbarOverlay // 继承 ToolbarOverlay，让 Overlay 内部可以组合 ToolbarElement。
    {
        public BurtShadingDebugOverlay() // 定义 Overlay 构造函数，Unity 创建 Overlay 时会调用。
            : base(BurtShadingDebugDropdown.Id) // 把下拉按钮的 ToolbarElement ID 注册到这个 Overlay 里。
        {
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))] // 把下拉按钮注册成 SceneView 可用的 ToolbarElement。
    internal sealed class BurtShadingDebugDropdown : EditorToolbarDropdownToggle, IAccessContainerWindow // 继承下拉 Toggle，并允许拿到宿主窗口。
    {
        public const string Id = "BurtRP/Shading Debug"; // 定义 ToolbarElement 唯一 ID，Overlay 会通过这个 ID 引用按钮。

        public EditorWindow containerWindow { get; set; } // 保存宿主窗口引用，IAccessContainerWindow 接口要求提供这个属性。

        public BurtShadingDebugDropdown() // 定义下拉按钮构造函数，Unity 创建工具栏元素时调用。
        {
            tooltip = "BurtRP Shading Debug"; // 设置鼠标悬停提示，方便识别这个按钮的用途。
            UpdateVisualState(); // 初始化按钮文字和选中状态，让 UI 反映当前 debug 模式。
            dropdownClicked += () => UnityEditor.PopupWindow.Show(worldBound, new BurtShadingDebugPopup(this)); // 明确使用 UnityEditor.PopupWindow，避免和 UIElements.PopupWindow 同名冲突。
            RegisterCallback<AttachToPanelEvent>(_ => UpdateVisualState()); // 按钮挂到面板时刷新一次，避免域重载后文字过期。
        }

        public void UpdateVisualState() // 根据当前 debug 模式刷新按钮显示。
        {
            var mode = BurtShadingDebugSettings.Mode; // 读取当前 runtime debug 模式。
            value = mode != BurtShadingDebugMode.None; // 非 None 时让 Toggle 处于开启状态。
            text = mode == BurtShadingDebugMode.None ? "Shading" : BurtShadingDebugDisplayNames.GetDisplayName(mode); // None 时显示通用标题，否则显示友好的 Debug 名。
        }
    }

    internal sealed class BurtShadingDebugPopup : PopupWindowContent // 定义 Overlay 下拉后显示的弹窗内容。
    {
        private const float ScrollMaxHeight = 440f; // 限制弹窗列表最大高度，Debug 模式继续增加时不会把 SceneView 菜单撑出屏幕。

        private readonly BurtShadingDebugDropdown owner; // 保存创建这个弹窗的下拉按钮，用来在切换模式后刷新按钮文字。

        private Vector2 scrollPosition; // 保存列表滚动位置，让分类后的长菜单可以在同一个弹窗里浏览。

        private sealed class BurtShadingDebugGroup // 定义一个菜单分类，对齐 XRender 按 Material / Lighting 等组拆分 Debug Overlay 的思路。
        {
            public BurtShadingDebugGroup(string title, BurtShadingDebugMode[] modes) // 构造一个带标题和模式列表的分类。
            {
                Title = title; // 保存显示在菜单里的分类标题。
                Modes = modes; // 保存这个分类下的所有 Debug 模式。
            }

            public string Title { get; } // 分类标题，例如 Material / Lighting。

            public BurtShadingDebugMode[] Modes { get; } // 分类下的 Debug 模式顺序。
        }

        private static readonly BurtShadingDebugGroup[] Groups = // 参考 XRender Editor/XShaderDebug/Groups：按 Material、Lighting、Deferred/Visualizer 等大类拆菜单。
        {
            new BurtShadingDebugGroup("General", new[] // 通用开关分类，用来快速回到正常渲染。
            {
                BurtShadingDebugMode.None // 正常渲染模式。
            }),

            new BurtShadingDebugGroup("Material / Generic Data", new[] // 对齐 XRender Material Debug：材质输入和 GenericData 派生值。
            {
                BurtShadingDebugMode.Albedo, // 材质基础色调试模式，会显示 BaseMap 和 BaseColor 合成后的 albedo。
                BurtShadingDebugMode.DiffuseColor, // XRender DiffuseColor 调试模式，用来检查 metallic 对 diffuse 的扣除。
                BurtShadingDebugMode.NormalWS, // 世界空间法线调试模式，会显示法线贴图影响后的最终 normalWS。
                BurtShadingDebugMode.Smoothness, // 光滑度调试模式，会显示标量和 Mask Map A 通道合成后的最终 smoothness。
                BurtShadingDebugMode.Roughness, // 粗糙度调试模式，用来检查 smoothness 到 perceptual roughness 的转换。
                BurtShadingDebugMode.Metallic, // 金属度调试模式，会显示标量和 Mask Map R 通道合成后的最终 metallic。
                BurtShadingDebugMode.Occlusion, // 环境遮蔽调试模式，用来检查 Mask Map G 通道和 Occlusion Strength。
                BurtShadingDebugMode.Reflectance, // Reflectance 调试模式，用来检查 XRender 风格反射率输入。
                BurtShadingDebugMode.F0, // F0 调试模式，用来检查 reflectance / metallic / baseColor 的还原结果。
                BurtShadingDebugMode.F90 // F90 调试模式，用来检查 Schlick Fresnel 的掠射端点。
            }),

            new BurtShadingDebugGroup("GBuffer / Deferred Data", new[] // Deferred 前置调试：只验证 BurtGBuffer 编解码，不写入真实 RenderTarget。
            {
                BurtShadingDebugMode.GBufferBaseColor, // GBuffer0.rgb 解码后的基础色。
                BurtShadingDebugMode.GBufferNormalWS, // GBuffer1.rg 解码后的世界空间法线。
                BurtShadingDebugMode.GBufferMetallic, // GBuffer1.b 解码后的 Metallic。
                BurtShadingDebugMode.GBufferSmoothness, // GBuffer1.a 解码后的 Smoothness。
                BurtShadingDebugMode.GBufferOcclusion, // GBuffer0.a 解码后的 AO。
                BurtShadingDebugMode.GBufferReflectance // GBuffer2.a 解码后的 XRender reflectance。
            }),

            new BurtShadingDebugGroup("Specular AA / Normal Filtering", new[] // 对齐 XRender CommonMaterial 的 NormalFiltering 调试链路。
            {
                BurtShadingDebugMode.SpecularAARoughness, // 高光 AA 粗糙度调试模式，用来观察高光是否被像素法线变化拓宽。
                BurtShadingDebugMode.SpecularAANormalVariance, // 高光 AA 法线方差调试模式，用来观察 Normal Filtering 输入。
                BurtShadingDebugMode.SpecularAARoughnessDelta // 高光 AA 粗糙度增量调试模式，用来观察 Specular AA 的实际影响。
            }),

            new BurtShadingDebugGroup("Direct BRDF", new[] // 对齐 XRender SlabLobes：把直接光 D / V / F / diffuse lobe 拆开。
            {
                BurtShadingDebugMode.DirectBRDFD, // 直接光 GGX D 项调试模式，用来检查高 smoothness 下的 NDF 峰值。
                BurtShadingDebugMode.DirectBRDFVisibility, // 直接光 Visibility 调试模式，用来检查 Smith Joint 几何遮蔽。
                BurtShadingDebugMode.DirectBRDFFresnel, // 直接光 Fresnel 调试模式，用来检查 F0 与视角项。
                BurtShadingDebugMode.DirectDiffuseLobe, // 直接光 diffuse lobe 调试模式，当前默认 Lambert，后续可切 Burley。
                BurtShadingDebugMode.DirectDiffuseBRDF, // 直接光 diffuse BRDF 调试模式，不含灯光颜色、NdotL 和阴影。
                BurtShadingDebugMode.DirectSpecularBRDF // 直接光 specular BRDF 调试模式，不含灯光颜色、NdotL 和阴影。
            }),

            new BurtShadingDebugGroup("IBL / Energy / Occlusion", new[] // 对齐 XRender Sky/EnvProbe Specular、EnergyCompensation 和 SpecularOcclusion。
            {
                BurtShadingDebugMode.IndirectSpecularDFG, // 间接高光 DFG 调试模式，用来检查 PreIntegratedFG。
                BurtShadingDebugMode.IndirectSpecularEnvBRDF, // 间接高光 EnvBRDF 调试模式，用来检查 DFG 应用到 F0/F90 后的结果。
                BurtShadingDebugMode.SpecularEnergyCompensation, // 高光能量补偿调试模式。
                BurtShadingDebugMode.IndirectSpecularEnergyCompensation, // 间接高光能量补偿调试模式。
                BurtShadingDebugMode.EnergyPreservation, // XRender 底层 diffuse 保能比例调试模式。
                BurtShadingDebugMode.SpecularOcclusion // 间接高光遮蔽调试模式。
            }),

            new BurtShadingDebugGroup("Lighting", new[] // 对齐 XRender Lighting Debug：最终 direct / indirect / total lighting 拆分。
            {
                BurtShadingDebugMode.DetailLighting, // Detail Lighting 调试模式，参考 XRender 用 0.18 中灰 BaseColor 观察光照细节。
                BurtShadingDebugMode.DirectDiffuse, // 直接漫反射调试模式，只显示主光 diffuse 贡献。
                BurtShadingDebugMode.DirectSpecular, // 直接高光调试模式，只显示主光 specular 贡献。
                BurtShadingDebugMode.IndirectLighting, // 间接光调试模式，只显示 SH 漫反射和 Reflection Probe 镜面反射。
                BurtShadingDebugMode.IndirectDiffuse, // 间接漫反射调试模式，只显示 SH / Light Probe diffuse 贡献。
                BurtShadingDebugMode.IndirectSpecular // 间接高光调试模式，只显示 Reflection Probe specular 贡献。
            }),

            new BurtShadingDebugGroup("Fullscreen / Render Data", new[] // BurtRP 现有全屏调试入口，类似 XRender 独立模块 Debug。
            {
                BurtShadingDebugMode.CameraDepth, // 复用已有 CameraDepth 全屏 debug pass。
                BurtShadingDebugMode.MainLightShadow // 复用已有 MainLightShadow 全屏 debug pass。
            })
        };

        public BurtShadingDebugPopup(BurtShadingDebugDropdown owner) // 定义弹窗构造函数。
        {
            this.owner = owner; // 保存下拉按钮引用，允许切换模式后刷新按钮文字。
        }

        public override Vector2 GetWindowSize() // 返回弹窗窗口大小。
        {
            float contentHeight = 30f + GetModeCount() * EditorGUIUtility.singleLineHeight + Groups.Length * 24f + 78f; // 根据模式和分类标题估算完整高度。
            return new Vector2(310f, Mathf.Min(contentHeight, ScrollMaxHeight + 108f)); // 列表过长时固定高度并启用滚动。
        }

        public override void OnGUI(Rect rect) // 绘制弹窗 GUI。
        {
            EditorGUILayout.LabelField("BurtRP Shading Debug", EditorStyles.boldLabel); // 绘制标题，说明这是 BurtRP 的调试菜单。

            float scrollHeight = Mathf.Min(ScrollMaxHeight, GetModeCount() * EditorGUIUtility.singleLineHeight + Groups.Length * 24f + 8f); // 计算列表可视高度。
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(scrollHeight)); // 用滚动列表承载分类后的 Debug 选项。

            foreach (var group in Groups) // 遍历所有 Debug 分类。
            {
                DrawGroup(group); // 绘制当前分类标题和它下面的模式。
            }

            EditorGUILayout.EndScrollView(); // 结束滚动区域，下面继续显示资产信息和说明。
            EditorGUILayout.Space(4f); // 加一点间距，让菜单项和资产信息分开。

            using (new EditorGUI.DisabledScope(true)) // 禁用下面的 ObjectField，只用于显示当前资产，不允许在这里拖拽修改。
            {
                var asset = BurtShadingDebugOverlayUtility.GetActiveBurtAsset(); // 通过公共工具方法查找当前正在使用的 BurtRenderPipelineAsset，避免在弹窗类里调用不存在的本地方法。
                EditorGUILayout.ObjectField("Active Asset", asset, typeof(BurtRenderPipelineAsset), false); // 显示当前管线资产，方便确认 Overlay 正在操作哪个 asset。
            }

            EditorGUILayout.HelpBox("材质/光照模式先写入全局 shader 参数；Depth 和 Shadow 会同步驱动现有 BurtRP 调试视图。", MessageType.Info); // 说明当前最小版本的行为边界。
        }

        private static int GetModeCount() // 统计所有分类中的 Debug 模式数量。
        {
            int count = 0; // 从 0 开始累计。

            foreach (var group in Groups) // 遍历每个分类。
            {
                count += group.Modes.Length; // 加上当前分类里的模式数量。
            }

            return count; // 返回总模式数量，用来估算弹窗高度。
        }

        private void DrawGroup(BurtShadingDebugGroup group) // 绘制一个 Debug 分类。
        {
            EditorGUILayout.Space(3f); // 分类之间留一点空隙，避免菜单看起来像一整串无分组列表。
            EditorGUILayout.LabelField(group.Title, EditorStyles.miniBoldLabel); // 使用小号粗体标题，接近 XRender 多个 Debug Dropdown 的分组感。

            foreach (var mode in group.Modes) // 遍历当前分类下的所有模式。
            {
                DrawMode(mode); // 为当前模式绘制一行可点击菜单项。
            }
        }

        private void DrawMode(BurtShadingDebugMode mode) // 绘制一个模式菜单项。
        {
            var isCurrent = BurtShadingDebugSettings.Mode == mode; // 判断这一行是否是当前模式。

            if (!GUILayout.Toggle(isCurrent, BurtShadingDebugDisplayNames.GetDisplayName(mode), "MenuItem")) // 用菜单样式绘制 Toggle 行，并判断是否被点击。
            {
                return; // 没有点击时直接返回，不改变模式。
            }

            if (!isCurrent) // 只有点击了非当前模式时才执行切换。
            {
                SetMode(mode); // 写入新模式并同步已有 debug view。
                editorWindow.Close(); // 切换后关闭弹窗，行为和普通菜单一致。
            }
        }

        private void SetMode(BurtShadingDebugMode mode) // 设置新的 shading debug 模式。
        {
            BurtShadingDebugSettings.Mode = mode; // 写入 runtime 静态状态，并上传 shader 全局参数。
            BurtShadingDebugOverlayUtility.SyncExistingDebugViews(mode); // 同步 BurtRP asset 上已有的 Depth/Shadow 调试开关。
            owner?.UpdateVisualState(); // 如果弹窗来自 Overlay 按钮，就刷新按钮文字和选中状态。
            SceneView.RepaintAll(); // 重绘所有 SceneView，让 debug view 切换尽快可见。
        }
    }

    internal static class BurtShadingDebugOverlayUtility // 定义 Overlay 和 fallback window 共用的小工具。
    {
        public static void SyncExistingDebugViews(BurtShadingDebugMode mode) // 根据当前模式同步 BurtRP 已有全屏 debug view 开关。
        {
            var asset = GetActiveBurtAsset(); // 查找当前渲染管线资产。

            if (asset == null) // 如果当前项目没有使用 BurtRP asset，就没有可同步的目标。
            {
                return; // 直接返回，避免 SerializedObject 接收空对象。
            }

            var serializedAsset = new SerializedObject(asset); // 用 SerializedObject 修改私有 SerializeField，避免给 asset 增加额外公开 setter。
            SetBool(serializedAsset, "enableDepthDebugView", mode == BurtShadingDebugMode.CameraDepth); // CameraDepth 模式开启已有深度调试 pass，其它模式关闭。
            SetBool(serializedAsset, "enableMainLightShadowDebugView", mode == BurtShadingDebugMode.MainLightShadow); // MainLightShadow 模式开启已有阴影图调试 pass，其它模式关闭。
            serializedAsset.ApplyModifiedPropertiesWithoutUndo(); // 应用修改但不压入 Undo，避免每次切 debug 都污染撤销栈。
            EditorUtility.SetDirty(asset); // 标记 asset 已修改，确保 Inspector 和序列化状态能刷新。
        }

        public static BurtRenderPipelineAsset GetActiveBurtAsset() // 查找当前真正生效的 BurtRenderPipelineAsset。
        {
            var asset = GraphicsSettings.currentRenderPipeline as BurtRenderPipelineAsset; // 优先读取 GraphicsSettings 当前管线资产。

            if (asset != null) // 如果 GraphicsSettings 已经返回 BurtRP asset，就直接使用它。
            {
                return asset; // 返回当前 GraphicsSettings 资产。
            }

            return QualitySettings.renderPipeline as BurtRenderPipelineAsset; // 如果 GraphicsSettings 没有，就尝试读取当前 Quality 级别覆盖的管线资产。
        }

        private static void SetBool(SerializedObject serializedObject, string propertyName, bool value) // 设置 SerializedObject 上的 bool 字段。
        {
            var property = serializedObject.FindProperty(propertyName); // 按字段名查找私有 SerializeField。

            if (property != null) // 找到字段时才写入，避免字段改名后直接抛异常。
            {
                property.boolValue = value; // 写入新的 bool 值。
            }
        }
    }

    internal sealed class BurtShadingDebugWindow : EditorWindow // 定义 fallback 编辑器窗口，Overlay API 不显示时还能操作同一套状态。
    {
        [MenuItem("Window/Rendering/BurtRP/Shading Debug")] // 注册菜单入口，路径放在 Rendering/BurtRP 下。
        private static void Open() // 打开 fallback 窗口。
        {
            GetWindow<BurtShadingDebugWindow>("Burt Shading Debug"); // 获取或创建窗口，并设置标题。
        }

        private void OnGUI() // 绘制 fallback 窗口内容。
        {
            EditorGUILayout.LabelField("Overlay Fallback", EditorStyles.boldLabel); // 绘制窗口标题。
            EditorGUILayout.HelpBox("如果 SceneView Overlay 菜单没有显示，可先用这个窗口确认同一套调试状态。", MessageType.Info); // 说明这个窗口是 Overlay 的备选入口。

            EditorGUI.BeginChangeCheck(); // 开始监听 EnumPopup 是否发生变化。
            var mode = (BurtShadingDebugMode)EditorGUILayout.EnumPopup("Mode", BurtShadingDebugSettings.Mode); // 绘制模式枚举选择框。

            if (EditorGUI.EndChangeCheck()) // 如果用户切换了模式，就同步状态。
            {
                BurtShadingDebugSettings.Mode = mode; // 写入 runtime 静态状态，并上传 shader 全局参数。
                BurtShadingDebugOverlayUtility.SyncExistingDebugViews(mode); // 同步已有 Depth/Shadow 全屏调试开关。
                SceneView.RepaintAll(); // 重绘 SceneView，让切换结果尽快显示。
            }

            EditorGUILayout.LabelField("Shader Mode", BurtShadingDebugSettings.ModeShaderName); // 显示 shader 模式属性名，方便后续接 shader 时核对。
            EditorGUILayout.LabelField("Shader Enabled", BurtShadingDebugSettings.EnabledShaderName); // 显示 shader 开关属性名，方便后续接 shader 时核对。
        }
    }
}
