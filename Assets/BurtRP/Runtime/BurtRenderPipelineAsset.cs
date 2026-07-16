using Sirenix.OdinInspector; // 引入 Odin Inspector 命名空间，用来给新配置提供更清晰的分组显示。
using System.Collections.Generic;
using UnityEngine.Experimental.Rendering;
using UnityEngine; // 引入 UnityEngine 命名空间，用来使用 Color、SerializeField、CreateAssetMenu 等 Unity 类型。
using UnityEngine.Rendering; // 引入 Unity 渲染命名空间，用来继承 RenderPipelineAsset。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让管线资产和其他 BurtRP 代码处在同一个模块里。
{
    public enum BurtRendererMode // 定义 BurtRP 当前使用哪一种主渲染路径。
    {
        Forward = 0, // 使用当前已经验证稳定的前向渲染路径，这是默认模式。
        Deferred = 1 // 使用 Deferred 实验路径，当前阶段只接入 GBuffer 资源生命周期并临时复用 Forward 输出。
    }

    public enum BurtGBufferDebugViewMode // 定义 Deferred GBuffer 调试视图要显示哪一种内容。
    {
        Disabled = 0, // 关闭 GBuffer 调试视图，保持正常渲染结果。
        GBuffer0 = 1, // 直接显示 GBuffer0 原始内容，用来检查 DepthNormals prepass 写入的 normal 和 roughness。
        GBuffer1 = 2, // 直接显示 GBuffer1 原始内容，用来检查 baseColor 和 occlusion 是否写入。
        GBuffer2 = 3, // 直接显示 GBuffer2 原始内容，用来检查 shading model/material channel、metallic、smoothness 和 reflectance 是否写入。
        GBuffer3 = 19,
        BaseColor = 4, // 解码后只显示材质基础色，方便和 Forward Lit 的颜色输入对齐。
        NormalWS = 5, // 解码后显示 GBuffer 向量槽；Default Lit=normalWS，Hair=strandDirectionWS。
        Metallic = 6, // 解码后显示 GBuffer 材质通道；Default Lit=metallic，Hair=scatter。
        Smoothness = 7, // 解码后显示光滑度灰度图，方便检查 smoothness 在 GBuffer 中是否反向或丢失。
        Occlusion = 8, // 解码后显示环境遮蔽灰度图，方便检查 occlusion 通道是否正确。
        Emission = 9, // 解码后显示自发光颜色，方便检查 HDR emission 是否写入 GBuffer4。
        Reflectance = 10, // 解码后显示 XRender 风格 reflectance 灰度图，方便检查非金属 F0 来源。
        RawDepth = 11, // 显示当前 CameraDepth 原始深度，方便把 GBuffer 和深度重建问题放在同一入口排查。
        Roughness = 12, // 解码后显示从 smoothness 还原的感知粗糙度，方便和 PBR BRDF 输入对齐。
        DiffuseColor = 13, // 解码后显示 GBuffer 重建出的 diffuseColor，方便检查 metallic 扣除后的漫反射颜色。
        ShadingModel = 14, // 解码后显示 shading model，黑色=Default Lit，洋红=Hair，方便验证材质是否进入 Hair 分支。
        HairStrandDirection = 15, // Hair 专用：显示 GBuffer0.rgb 解码后的 strand direction，非 Hair 像素显示黑色。
        HairScatter = 16, // Hair 专用：显示复用 GBuffer2.r material channel 解码出的 scatter，非 Hair 像素显示黑色。
        HairShift = 17, // Hair 专用：显示复用 GBuffer2.r material channel 解码出的 longitudinal shift scale，非 Hair 像素显示黑色。
        SubsurfaceStrength = 18, // Subsurface 专用：显示复用 GBuffer2.r material channel 解码出的 strength。
        ClearCoatNormalWS = 20,
        ClearCoatMask = 21,
        ClearCoatRoughness = 22,
        GBuffer4 = 23,
        GBuffer5 = 36,
        Anisotropy = 24,
        TangentWS = 25,
        SubsurfaceThickness = 26,
        SubsurfaceProfileIndex = 27,
        FoliageTransmissionColor = 28,
        FoliageTransmissionWeight = 29,
        FoliageThickness = 30,
        FoliageTransmissionNdotL = 31,
        FoliageSpecularScale = 32,
        FoliageScreenSpaceShadowIntensity = 33,
        GrassIsGrass = 37,
        GrassSSSIntensity = 38,
        GrassSpecularMultiply = 39,
        GrassScreenSpaceShadowIntensity = 40,
        StencilRaw = 34,
        StencilShadingModel = 35
    }

    [CreateAssetMenu(menuName = "Rendering/Burt Render Pipeline Asset", fileName = "BurtRenderPipelineAsset")] // 让 Unity 可以通过 Create 菜单创建 BurtRenderPipelineAsset。
    public sealed class BurtRenderPipelineAsset : RenderPipelineAsset // 定义 BurtRP 的管线资产，Unity Graphics Settings 会引用它来创建管线实例。
    {
        private const string DefaultMaterialAssetPath = "Assets/BurtRP/Runtime/Materials/MI_StandardLit.mat";
        private const string DefaultMaterialFallbackShaderName = "BurtRP/Lit";
        private const string XGIRadianceCacheHardwareRayTracingResourcePath = "BurtGIRadianceCacheHardwareRayTracing";
        private static Material cachedDefaultMaterial;

        [TitleGroup("Pipeline - 管线")] // 使用 Odin 给管线级配置建立独立分组，方便后续继续放 Renderer Mode、MSAA 等核心开关。
        [SerializeField] private BurtRendererMode rendererMode = BurtRendererMode.Forward; // 定义当前管线使用的渲染路径，默认 Forward，避免新增 Deferred 代码后改变现有画面。

        [SerializeField] private Color clearColor = new Color(0.02f, 0.02f, 0.025f, 1f); // 定义默认清屏颜色，并暴露到 Inspector 供你调整。

        [SerializeField] private bool enableDepthPrepass = true; // 定义是否启用 Depth Prepass，默认开启，方便当前阶段观察深度预写流程。

        [SerializeField] private bool enableDepthDebugView = false; // 定义是否把 CameraDepth 可视化到最终颜色目标，默认关闭以避免覆盖正常画面。

        [SerializeField] private float depthDebugScale = 50f; // 定义深度可视化的亮度缩放，数值越大越容易看清近处深度变化。

        [TitleGroup("Deferred - 延迟渲染")] // 使用 Odin 给 Deferred 实验功能建立独立分组，避免和稳定的 Forward 配置混在一起。
        [ShowIf(nameof(IsDeferredRendererMode))] // 只有 Renderer Mode 切到 Deferred 时才显示这个临时兼容开关。
        [SerializeField, LabelText("启用 ForwardOnly 不透明兜底")] private bool enableDeferredForwardOpaqueFallback = true; // 定义 Deferred Lighting 后是否绘制 LightMode=BurtForwardOnly 的不透明物体，避免不能写 GBuffer 的 BurtRP shader 在 Deferred 下消失。

        [TitleGroup("Deferred - 延迟渲染")] // 继续使用同一个 Deferred 分组，方便集中调试 GBuffer 和 Deferred Lighting。
        [ShowIf(nameof(IsDeferredRendererMode))] // 只有 Deferred 模式才显示 GBuffer 调试选项，因为 Forward 路径不会申请 GBuffer。
        [SerializeField] private BurtGBufferDebugViewMode gBufferDebugViewMode = BurtGBufferDebugViewMode.Disabled; // 定义 GBuffer 调试视图模式，默认关闭以保持正常画面。

        [TitleGroup("Deferred - 延迟渲染")]
        [ShowIf(nameof(IsDeferredRendererMode))]
        [SerializeField, LabelText("启用 HiZ Debug View")] private bool enableHiZDebugView = false;

        [TitleGroup("Deferred - 延迟渲染")]
        [ShowIf(nameof(IsDeferredRendererMode))]
        [SerializeField, Min(0)] private int hiZDebugMip = 0;

        [TitleGroup("Deferred - 延迟渲染")]
        [ShowIf(nameof(IsDeferredRendererMode))]
        [SerializeField, Min(0.0001f)] private float hiZDebugScale = 50f;

        [Header("PBR / Shading")] // 把 PBR 共享查找表集中显示，方便确认 BRDF 使用的全局资源。
        [SerializeField] private Texture2D preintegratedFGLut; // 保存预积分 FG LUT，默认指向 Assets/Textures/PreintegratedFG.exr。
        [TitleGroup("XGI - Hardware Ray Tracing")]
        [SerializeField] private bool enableXGIHardwareRayTracing;
        [TitleGroup("XGI - Hardware Ray Tracing")]
        [ShowIf(nameof(enableXGIHardwareRayTracing))]
        [SerializeField] private RayTracingShader xgiRadianceCacheHardwareRayTracingShader;
        private RayTracingShader cachedXGIRadianceCacheHardwareRayTracingShader;

        [TitleGroup("Deferred - 屏幕空间次表面 5S")]
        [ShowIf(nameof(IsDeferredRendererMode))]
        [SerializeField, LabelText("启用 5S")] private bool enableScreenSpaceSubsurface = true;

        [TitleGroup("Deferred - 屏幕空间次表面 5S")]
        [ShowIf(nameof(IsDeferredRendererMode))]
        [SerializeField, LabelText("5S Profile")] private BurtSubsurfaceProfile screenSpaceSubsurfaceProfile;

        [TitleGroup("Deferred - 屏幕空间次表面 5S")]
        [ShowIf(nameof(IsDeferredRendererMode))]
        [SerializeField, LabelText("5S Profile List")] private List<BurtSubsurfaceProfile> screenSpaceSubsurfaceProfiles = new List<BurtSubsurfaceProfile>();

        [TitleGroup("Deferred - 屏幕空间次表面 5S")]
        [ShowIf(nameof(IsDeferredRendererMode))]
        [SerializeField, Min(0.01f), LabelText("半径像素")] private float screenSpaceSubsurfaceRadiusPixels = 3.25f;

        [TitleGroup("Deferred - 屏幕空间次表面 5S")]
        [ShowIf(nameof(IsDeferredRendererMode))]
        [SerializeField, Min(0.0001f), LabelText("深度容差")] private float screenSpaceSubsurfaceDepthSigma = 0.08f;

        [TitleGroup("Deferred - 屏幕空间次表面 5S")]
        [ShowIf(nameof(IsDeferredRendererMode))]
        [SerializeField, Range(0.01f, 1f), LabelText("法线容差")] private float screenSpaceSubsurfaceNormalSigma = 0.72f;

        [TitleGroup("Deferred - 屏幕空间次表面 5S")]
        [ShowIf(nameof(IsDeferredRendererMode))]
        [SerializeField, Range(0f, 1f), LabelText("混合强度")] private float screenSpaceSubsurfaceBlend = 0.85f;

        [TitleGroup("Deferred - 屏幕空间次表面 5S")]
        [ShowIf(nameof(IsDeferredRendererMode))]
        [SerializeField, Min(0.01f), LabelText("距离缩放")] private float screenSpaceSubsurfaceDistanceScale = 2f;

        [TitleGroup("Deferred - 屏幕空间次表面 5S")]
        [ShowIf(nameof(IsDeferredRendererMode))]
        [SerializeField, Range(0f, 1f), LabelText("边界防串色")] private float screenSpaceSubsurfaceBoundaryBleed = 0.25f;

        [TitleGroup("Deferred - 屏幕空间次表面 5S")]
        [ShowIf(nameof(IsDeferredRendererMode))]
        [SerializeField, Range(0f, 1f), LabelText("染色强度")] private float screenSpaceSubsurfaceTintStrength = 0.35f;

        [TitleGroup("Deferred - 屏幕空间次表面 5S")]
        [ShowIf(nameof(IsDeferredRendererMode))]
        [SerializeField, Range(0f, 0.2f), LabelText("最小有效强度")] private float screenSpaceSubsurfaceMinStrength = 0.012f;

        [TitleGroup("Post Processing - 后处理")] // 使用 Odin 给后处理配置建立独立分组；这里不用斜杠，避免 Odin 把斜杠解析成父子分组路径。
        [SerializeField, InlineProperty, HideLabel] private PostProcessSettings postProcessSettings = new PostProcessSettings(); // 保存 BurtRP 后处理框架设置；具体效果参数从 Global Volume 读取。

        [TitleGroup("Post Processing - 后处理")] // 和后处理框架开关放在同一组，表示这是管线级 Volume 查询配置。
        [SerializeField] private LayerMask postProcessVolumeLayerMask = ~0; // 定义后处理 Global Volume 查询层，默认所有层都能参与 BurtRP 后处理。

        [SerializeField] private bool enableUnsupportedShaderDebug = true; // 定义是否绘制不支持的 Shader 为错误材质，方便迁移材质时立刻发现漏改的 shader。

        [SerializeField] private bool enableRenderGraphDebug = false; // 定义 RenderGraph 调试捕获开关，默认关闭，避免每帧生成长文本。

        [SerializeField] private bool enableRenderGraphDebugConsoleLog = false; // 定义是否把捕获到的 RenderGraph Debug 继续输出到 Console；默认关闭，优先走剪切板按钮。

        [Header("Camera Debug")] // 把相机相关调试开关单独分组，避免和阴影、深度等其他模块混在一起。
        [SerializeField] private bool enableCameraSortDebugLog = false; // 定义是否输出相机 request 排序列表，默认关闭，避免每帧多相机时刷 Console。
        [SerializeField] private bool enableRenderFrameDebugLog = false; // 定义是否输出 Frame/Stack 分组日志，默认关闭，避免每帧打印相机栈诊断。

        public Color ClearColor => clearColor; // 暴露默认清屏颜色给渲染 Pass 使用。

        public override Material defaultMaterial // Unity 创建默认 3D 物体时查询的 SRP 默认材质。
        {
            get
            {
#if UNITY_EDITOR
                if (cachedDefaultMaterial == null)
                {
                    cachedDefaultMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(DefaultMaterialAssetPath);
                }
#endif
                if (cachedDefaultMaterial == null)
                {
                    Shader shader = Shader.Find(DefaultMaterialFallbackShaderName);
                    if (shader != null)
                    {
                        cachedDefaultMaterial = new Material(shader)
                        {
                            name = "BurtRP Default Material",
                            hideFlags = HideFlags.HideAndDontSave
                        };
                    }
                }

                return cachedDefaultMaterial;
            }
        }

        public BurtRendererMode RendererMode => rendererMode; // 暴露当前渲染路径给 BurtRenderPipeline 和 RenderGraph 资源注册逻辑使用。

        public bool EnableDepthPrepass => enableDepthPrepass; // 暴露 Depth Prepass 开关给 Graph Assembler 使用。

        public bool EnableDepthDebugView => enableDepthDebugView; // 暴露深度可视化开关给 Graph Assembler 使用。

        public float DepthDebugScale => Mathf.Max(0.0001f, depthDebugScale); // 暴露经过保护的深度可视化缩放，避免 shader 收到 0 或负数。

        public bool EnableDeferredForwardOpaqueFallback => enableDeferredForwardOpaqueFallback; // 暴露 Deferred 的 ForwardOnly 不透明兜底开关，让组装器可以绘制不能写入 GBuffer 的专用前向物体。

        public BurtGBufferDebugViewMode GBufferDebugViewMode => gBufferDebugViewMode; // 暴露当前 GBuffer 调试视图模式，让 Deferred Debug Pass 知道要显示哪一种内容。

        public bool EnableHiZDebugView => enableHiZDebugView;

        public int HiZDebugMip => Mathf.Max(0, hiZDebugMip);

        public float HiZDebugScale => Mathf.Max(0.0001f, hiZDebugScale);

        public Texture2D PreintegratedFGLut => preintegratedFGLut; // 暴露预积分 FG LUT，RenderPipeline 会把它绑定成全局 shader 纹理。
        public bool XGIHardwareRayTracingEnabledInAsset => enableXGIHardwareRayTracing;
        public bool EnableXGIHardwareRayTracing => enableXGIHardwareRayTracing && SystemInfo.supportsRayTracing && XGIRadianceCacheHardwareRayTracingShader != null;
        public RayTracingShader XGIRadianceCacheHardwareRayTracingShader => ResolveXGIRadianceCacheHardwareRayTracingShader();

        public bool EnableScreenSpaceSubsurface => enableScreenSpaceSubsurface;

        public BurtSubsurfaceProfile ScreenSpaceSubsurfaceProfile => screenSpaceSubsurfaceProfile;

        public BurtSubsurfaceProfileSettings ScreenSpaceSubsurfaceProfileSettings => ResolveScreenSpaceSubsurfaceSettings();

        public int ScreenSpaceSubsurfaceProfileMaxCount => BurtSubsurfaceProfilePalette.MaxProfiles;

        public int ScreenSpaceSubsurfaceProfileCount => ResolveScreenSpaceSubsurfaceProfilePalette().Count;

        public IReadOnlyList<BurtSubsurfaceProfileSettings> ScreenSpaceSubsurfaceProfileSettingsList => ResolveScreenSpaceSubsurfaceProfilePalette().Settings;

        public BurtSubsurfaceProfilePalette ScreenSpaceSubsurfaceProfilePalette => ResolveScreenSpaceSubsurfaceProfilePalette();

        public float ScreenSpaceSubsurfaceRadiusPixels => ScreenSpaceSubsurfaceProfileSettings.RadiusPixels;

        public float ScreenSpaceSubsurfaceDepthSigma => ScreenSpaceSubsurfaceProfileSettings.DepthSigma;

        public float ScreenSpaceSubsurfaceNormalSigma => ScreenSpaceSubsurfaceProfileSettings.NormalSigma;

        public float ScreenSpaceSubsurfaceBlend => ScreenSpaceSubsurfaceProfileSettings.Blend;

        public float ScreenSpaceSubsurfaceDistanceScale => ScreenSpaceSubsurfaceProfileSettings.DistanceScale;

        public float ScreenSpaceSubsurfaceBoundaryBleed => ScreenSpaceSubsurfaceProfileSettings.BoundaryBleed;

        public float ScreenSpaceSubsurfaceTintStrength => ScreenSpaceSubsurfaceProfileSettings.TintStrength;

        public float ScreenSpaceSubsurfaceMinStrength => ScreenSpaceSubsurfaceProfileSettings.MinStrength;

        public PostProcessSettings PostProcessSettings => EnsurePostProcessSettings(); // 暴露后处理设置给 RenderGraph 和 ForwardGraph 使用，并确保旧资产缺失字段时也有安全默认值。

        public LayerMask PostProcessVolumeLayerMask => postProcessVolumeLayerMask; // 暴露后处理 Volume 查询层给 VolumeManager.Update 使用。

        public bool EnableUnsupportedShaderDebug => enableUnsupportedShaderDebug; // 暴露不支持 Shader 调试开关给 Graph Assembler 使用。

        public bool EnableRenderGraphDebug => enableRenderGraphDebug; // 暴露 RenderGraph 调试开关给 BurtCameraRenderer 使用。

        public bool EnableRenderGraphDebugConsoleLog => enableRenderGraphDebugConsoleLog; // 暴露 RenderGraph Console 输出开关，让渲染器决定是否继续打印长日志。

        public bool EnableCameraSortDebugLog => enableCameraSortDebugLog; // 暴露相机排序调试开关给 BurtRenderPipeline 使用，只有打开时才会输出每帧 request 列表。

        public bool EnableRenderFrameDebugLog => enableRenderFrameDebugLog; // 暴露 Frame/Stack 分组调试开关给 BurtRenderPipeline 使用，只诊断分组不改变渲染结果。

        public bool HasLatestRenderGraphDebugDump => BurtRenderGraphDebugClipboardUtility.HasLatestDump; // 暴露最近一次 RenderGraph Debug 是否已经缓存，供自定义 Inspector 控制按钮启用状态。

        public string LatestRenderGraphDebugDumpSummary => BurtRenderGraphDebugClipboardUtility.LatestDumpSummary; // 暴露最近一次 RenderGraph Debug 摘要，供 Inspector 显示给用户确认。

        public bool HasLatestRenderGraphDebugDumpForRequestType(BurtRenderRequestType requestType) // 查询指定 request 类型是否已有缓存。
        {
            return BurtRenderGraphDebugClipboardUtility.HasLatestDumpForRequestType(requestType); // 转发到按类型缓存工具，避免 Inspector 直接依赖静态实现细节。
        }

        public string GetLatestRenderGraphDebugDumpSummary(BurtRenderRequestType requestType) // 获取指定 request 类型的最近一次 dump 摘要。
        {
            return BurtRenderGraphDebugClipboardUtility.GetLatestDumpSummary(requestType); // 返回 SceneView/Preview/Reflection 各自的摘要。
        }

        public void RequestCopyNextRenderGraphDebugDumpToClipboard() // 请求下一次渲染图 dump 生成后自动复制到剪切板。
        {
            BurtRenderGraphDebugClipboardUtility.RequestCopyNextDumpToClipboard(); // 把一次性复制请求交给静态缓存工具，下一次 request 渲染时会消费它。
        }

        public void RequestCopyNextRenderGraphDebugDumpToClipboard(BurtRenderRequestType requestType) // 请求下一次指定 request 类型的渲染图 dump 自动复制到剪切板。
        {
            BurtRenderGraphDebugClipboardUtility.RequestCopyNextDumpToClipboard(requestType); // 设置按 request 类型过滤的一次性复制请求。
        }

        public bool CopyLatestRenderGraphDebugDumpToClipboard() // 复制最近一次 RenderGraph Debug 到系统剪切板。
        {
            return BurtRenderGraphDebugClipboardUtility.CopyLatestDumpToClipboard(); // 复用剪切板工具的复制逻辑，并把是否成功返回给 Inspector。
        }

        public bool CopyLatestRenderGraphDebugDumpToClipboard(BurtRenderRequestType requestType) // 复制指定 request 类型最近一次 RenderGraph Debug 到系统剪切板。
        {
            return BurtRenderGraphDebugClipboardUtility.CopyLatestDumpToClipboard(requestType); // 复用按类型复制逻辑，避免 SceneView 覆盖 Preview/Reflection。
        }

        public void ClearLatestRenderGraphDebugDump() // 清空最近一次 RenderGraph Debug 缓存。
        {
            BurtRenderGraphDebugClipboardUtility.ClearLatestDump(); // 复用剪切板工具清空完整文本、摘要和一次性请求。
        }

        private RayTracingShader ResolveXGIRadianceCacheHardwareRayTracingShader()
        {
            if (xgiRadianceCacheHardwareRayTracingShader != null)
            {
                return xgiRadianceCacheHardwareRayTracingShader;
            }

            if (cachedXGIRadianceCacheHardwareRayTracingShader == null)
            {
                cachedXGIRadianceCacheHardwareRayTracingShader = Resources.Load<RayTracingShader>(XGIRadianceCacheHardwareRayTracingResourcePath);
            }

            return cachedXGIRadianceCacheHardwareRayTracingShader;
        }

        public string GetScreenSpaceSubsurfaceProfileName(int index)
        {
            return ResolveScreenSpaceSubsurfaceProfilePalette().GetName(index);
        }

        public BurtSubsurfaceProfile GetScreenSpaceSubsurfaceProfileAsset(int index)
        {
            if (index <= 0)
            {
                return screenSpaceSubsurfaceProfile;
            }

            EnsureScreenSpaceSubsurfaceProfileList();
            var listIndex = index - 1;
            return listIndex >= 0 && listIndex < screenSpaceSubsurfaceProfiles.Count
                ? screenSpaceSubsurfaceProfiles[listIndex]
                : null;
        }

        public int GetScreenSpaceSubsurfaceProfileIndex(BurtSubsurfaceProfile profile)
        {
            if (profile == null || profile == screenSpaceSubsurfaceProfile)
            {
                return 0;
            }

            EnsureScreenSpaceSubsurfaceProfileList();
            for (var i = 0; i < Mathf.Min(screenSpaceSubsurfaceProfiles.Count, BurtSubsurfaceProfilePalette.MaxProfiles - 1); i++)
            {
                if (screenSpaceSubsurfaceProfiles[i] == profile)
                {
                    return i + 1;
                }
            }

            return 0;
        }

        public int EnsureScreenSpaceSubsurfaceProfileSlot(BurtSubsurfaceProfile profile)
        {
            if (profile == null || profile == screenSpaceSubsurfaceProfile)
            {
                return 0;
            }

            EnsureScreenSpaceSubsurfaceProfileList();
            var existingIndex = GetScreenSpaceSubsurfaceProfileIndex(profile);
            if (existingIndex > 0)
            {
                return existingIndex;
            }

            var maxAdditionalProfiles = BurtSubsurfaceProfilePalette.MaxProfiles - 1;
            for (var i = 0; i < Mathf.Min(screenSpaceSubsurfaceProfiles.Count, maxAdditionalProfiles); i++)
            {
                if (screenSpaceSubsurfaceProfiles[i] == null)
                {
                    screenSpaceSubsurfaceProfiles[i] = profile;
                    return i + 1;
                }
            }

            if (screenSpaceSubsurfaceProfiles.Count < maxAdditionalProfiles)
            {
                screenSpaceSubsurfaceProfiles.Add(profile);
                return screenSpaceSubsurfaceProfiles.Count;
            }

            return 0;
        }

        private bool IsDeferredRendererMode => rendererMode == BurtRendererMode.Deferred; // 提供给 Odin ShowIf 使用，让 Deferred 专属配置只在 Deferred 模式下显示。

        private BurtSubsurfaceProfileSettings ResolveScreenSpaceSubsurfaceSettings()
        {
            return BurtSubsurfaceProfileSettings.Resolve(
                screenSpaceSubsurfaceProfile,
                screenSpaceSubsurfaceRadiusPixels,
                screenSpaceSubsurfaceDepthSigma,
                screenSpaceSubsurfaceNormalSigma,
                screenSpaceSubsurfaceBlend,
                screenSpaceSubsurfaceDistanceScale,
                screenSpaceSubsurfaceBoundaryBleed,
                screenSpaceSubsurfaceTintStrength,
                screenSpaceSubsurfaceMinStrength);
        }

        private BurtSubsurfaceProfilePalette ResolveScreenSpaceSubsurfaceProfilePalette()
        {
            EnsureScreenSpaceSubsurfaceProfileList();
            return BurtSubsurfaceProfilePalette.Resolve(
                ResolveScreenSpaceSubsurfaceSettings(),
                screenSpaceSubsurfaceProfiles);
        }

        private PostProcessSettings EnsurePostProcessSettings() // 定义后处理设置兜底函数，避免旧资产还没有序列化新字段时返回空引用。
        {
            if (postProcessSettings == null) // 如果 Unity 还没有给旧资产创建后处理设置对象，就在访问时补一个默认实例。
            {
                postProcessSettings = new PostProcessSettings(); // 创建默认后处理设置，默认关闭后处理框架以保持旧画面不变。
            }

            return postProcessSettings; // 返回可用的后处理设置对象，供外部只读访问。
        }

        protected override void OnValidate() // 重写 RenderPipelineAsset 的 OnValidate，避免隐藏基类同名函数产生 CS0114 警告。
        {
            base.OnValidate(); // 先执行 Unity 管线资产自己的校验逻辑，保持 RenderPipelineAsset 内部刷新行为不丢失。
            EnsurePostProcessSettings(); // 确保后处理设置对象存在，避免旧资产在 Inspector 中显示为空。
            EnsureScreenSpaceSubsurfaceProfileList();
            if (screenSpaceSubsurfaceProfiles.Count > BurtSubsurfaceProfilePalette.MaxProfiles - 1)
            {
                screenSpaceSubsurfaceProfiles.RemoveRange(
                    BurtSubsurfaceProfilePalette.MaxProfiles - 1,
                    screenSpaceSubsurfaceProfiles.Count - (BurtSubsurfaceProfilePalette.MaxProfiles - 1));
            }
        }

        private void EnsureScreenSpaceSubsurfaceProfileList()
        {
            if (screenSpaceSubsurfaceProfiles == null)
            {
                screenSpaceSubsurfaceProfiles = new List<BurtSubsurfaceProfile>();
            }
        }

        protected override UnityEngine.Rendering.RenderPipeline CreatePipeline() // Unity 会调用这个函数来创建真正运行时的 RenderPipeline 实例。
        {
            return new BurtRenderPipeline(this); // 创建 BurtRenderPipeline，并把当前资产传进去作为配置来源。
        }
    }
}
