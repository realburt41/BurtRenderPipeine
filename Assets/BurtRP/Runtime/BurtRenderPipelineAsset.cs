using Sirenix.OdinInspector; // 引入 Odin Inspector 命名空间，用来给新配置提供更清晰的分组显示。
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
        GBuffer0 = 1, // 直接显示 GBuffer0 原始内容，用来检查 baseColor 和 occlusion 是否写入。
        GBuffer1 = 2, // 直接显示 GBuffer1 原始内容，用来检查 oct normal、metallic 和 smoothness 是否写入。
        GBuffer2 = 3, // 直接显示 GBuffer2 原始内容，用来检查 emission 和 reflectance 是否写入。
        BaseColor = 4, // 解码后只显示材质基础色，方便和 Forward Lit 的颜色输入对齐。
        NormalWS = 5, // 解码后显示世界空间法线，方便检查法线编码和切线空间转换方向。
        Metallic = 6, // 解码后显示金属度灰度图，方便检查 MaskMap 的 metallic 通道。
        Smoothness = 7, // 解码后显示光滑度灰度图，方便检查 smoothness 在 GBuffer 中是否反向或丢失。
        Occlusion = 8, // 解码后显示环境遮蔽灰度图，方便检查 occlusion 通道是否正确。
        Emission = 9, // 解码后显示自发光颜色，方便检查 HDR emission 是否写入 GBuffer2。
        Reflectance = 10, // 解码后显示 XRender 风格 reflectance 灰度图，方便检查非金属 F0 来源。
        RawDepth = 11, // 显示当前 CameraDepth 原始深度，方便把 GBuffer 和深度重建问题放在同一入口排查。
        Roughness = 12, // 解码后显示从 smoothness 还原的感知粗糙度，方便和 PBR BRDF 输入对齐。
        DiffuseColor = 13 // 解码后显示 GBuffer 重建出的 diffuseColor，方便检查 metallic 扣除后的漫反射颜色。
    }

    public enum BurtShadowDebugYFlipMode // 定义主光 shadow map 调试图的 Y 翻转模式，避免在不同窗口和平台之间继续硬猜方向。
    {
        MatchFinalBlit = 0, // 使用和 Depth Debug 一样的 FinalBlit 预翻转规则，作为默认调试方向。
        InvertFinalBlit = 1, // 使用 FinalBlit 规则的反向结果，用来快速验证 shadow map 源纹理是否额外倒置。
        ForceNoFlip = 2, // 强制不翻转 shadow map 调试采样，方便排查具体平台的纹理原点。
        ForceFlip = 3 // 强制翻转 shadow map 调试采样，方便排查具体平台的纹理原点。
    }

    [CreateAssetMenu(menuName = "Rendering/Burt Render Pipeline Asset", fileName = "BurtRenderPipelineAsset")] // 让 Unity 可以通过 Create 菜单创建 BurtRenderPipelineAsset。
    public sealed class BurtRenderPipelineAsset : RenderPipelineAsset // 定义 BurtRP 的管线资产，Unity Graphics Settings 会引用它来创建管线实例。
    {
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

        [Header("PBR / Shading")] // 把 PBR 共享查找表集中显示，方便确认 BRDF 使用的全局资源。
        [SerializeField] private Texture2D preintegratedFGLut; // 保存预积分 FG LUT，默认指向 Assets/Textures/PreintegratedFG.exr。

        [TitleGroup("Post Processing - 后处理")] // 使用 Odin 给后处理配置建立独立分组；这里不用斜杠，避免 Odin 把斜杠解析成父子分组路径。
        [SerializeField, InlineProperty, HideLabel] private BurtPostProcessSettings postProcessSettings = new BurtPostProcessSettings(); // 保存 BurtRP 后处理框架设置；具体效果参数从 Global Volume 读取。

        [TitleGroup("Post Processing - 后处理")] // 和后处理框架开关放在同一组，表示这是管线级 Volume 查询配置。
        [SerializeField] private LayerMask postProcessVolumeLayerMask = ~0; // 定义后处理 Global Volume 查询层，默认所有层都能参与 BurtRP 后处理。

        [Header("Main Light Shadows")] // 把主光阴影配置集中显示在 Inspector，便于按项目需求统一调试。
        [SerializeField] private bool enableMainLightShadows = true; // 定义 BurtRP 是否允许渲染主方向光阴影；关闭后即使 Light 开了 Shadow 也不会申请 shadow map。

        [SerializeField, Min(16f)] private int mainLightShadowResolution = BurtShadowData.DefaultMainLightShadowResolution; // 定义主光阴影默认分辨率；Light 没有自定义分辨率时使用这个 SRP 级默认值。

        [SerializeField, Min(0f)] private float mainLightShadowDistance = BurtShadowData.DefaultMainLightShadowDistance; // 定义主光阴影最大剔除距离，CreateCameraRequest 会把它写入 cullingParameters.shadowDistance。

        [SerializeField, Min(0f)] private float mainLightShadowDepthBias = BurtShadowData.DefaultMainLightShadowDepthBias; // 定义写入 shadow map 时使用的常量深度偏移，用来减少表面自阴影 acne。

        [SerializeField, Min(0f)] private float mainLightShadowNormalBias = BurtShadowData.DefaultMainLightShadowNormalBias; // 定义写入 shadow map 时使用的顶点 normal bias，掠射角表面会沿法线获得更强偏移保护。

        [SerializeField, Min(0f)] private float mainLightShadowSampleBias = BurtShadowData.DefaultMainLightShadowSampleBias; // 定义接收端采样 shadow map 前减去的深度偏移，用来兜底处理轻微自遮挡。

        [SerializeField] private bool enableMainLightShadowDebugView = false; // 定义是否把主光 shadow map 直接画到 CameraColor，方便确认阴影图是否真的写入内容。

        [SerializeField, Min(0.0001f)] private float mainLightShadowDebugExposure = 1f; // 定义 shadow map 调试视图亮度倍率，贴图过暗或过亮时可以直接在资产上调整。

        [SerializeField] private BurtShadowDebugYFlipMode mainLightShadowDebugYFlipMode = BurtShadowDebugYFlipMode.MatchFinalBlit; // 定义主光 shadow map 调试图的 Y 翻转模式，默认先和 Depth Debug 使用同一套最终输出规则。

        [SerializeField] private bool enableMainLightShadowDebugLog = false; // 定义是否输出主光阴影诊断日志；默认关闭，避免每帧每相机刷 Console。

        [SerializeField] private bool enableUnsupportedShaderDebug = true; // 定义是否绘制不支持的 Shader 为错误材质，方便迁移材质时立刻发现漏改的 shader。

        [SerializeField] private bool enableRenderGraphDebug = false; // 定义 RenderGraph 调试捕获开关，默认关闭，避免每帧生成长文本。

        [SerializeField] private bool enableRenderGraphDebugConsoleLog = false; // 定义是否把捕获到的 RenderGraph Debug 继续输出到 Console；默认关闭，优先走剪切板按钮。

        [Header("Camera Debug")] // 把相机相关调试开关单独分组，避免和阴影、深度等其他模块混在一起。
        [SerializeField] private bool enableCameraSortDebugLog = false; // 定义是否输出相机 request 排序列表，默认关闭，避免每帧多相机时刷 Console。
        [SerializeField] private bool enableRenderFrameDebugLog = false; // 定义是否输出 Frame/Stack 分组日志，默认关闭，避免每帧打印相机栈诊断。

        public Color ClearColor => clearColor; // 暴露默认清屏颜色给渲染 Pass 使用。

        public BurtRendererMode RendererMode => rendererMode; // 暴露当前渲染路径给 BurtRenderPipeline 和 RenderGraph 资源注册逻辑使用。

        public bool EnableDepthPrepass => enableDepthPrepass; // 暴露 Depth Prepass 开关给 Graph Assembler 使用。

        public bool EnableDepthDebugView => enableDepthDebugView; // 暴露深度可视化开关给 Graph Assembler 使用。

        public float DepthDebugScale => Mathf.Max(0.0001f, depthDebugScale); // 暴露经过保护的深度可视化缩放，避免 shader 收到 0 或负数。

        public bool EnableDeferredForwardOpaqueFallback => enableDeferredForwardOpaqueFallback; // 暴露 Deferred 的 ForwardOnly 不透明兜底开关，让组装器可以绘制不能写入 GBuffer 的专用前向物体。

        public BurtGBufferDebugViewMode GBufferDebugViewMode => gBufferDebugViewMode; // 暴露当前 GBuffer 调试视图模式，让 Deferred Debug Pass 知道要显示哪一种内容。

        public Texture2D PreintegratedFGLut => preintegratedFGLut; // 暴露预积分 FG LUT，RenderPipeline 会把它绑定成全局 shader 纹理。

        public BurtPostProcessSettings PostProcessSettings => EnsurePostProcessSettings(); // 暴露后处理设置给 RenderGraph 和 ForwardGraph 使用，并确保旧资产缺失字段时也有安全默认值。

        public LayerMask PostProcessVolumeLayerMask => postProcessVolumeLayerMask; // 暴露后处理 Volume 查询层给 VolumeManager.Update 使用。

        public bool EnableMainLightShadows => enableMainLightShadows; // 暴露主光阴影总开关，让阴影数据和 Pass 组装都能统一判断是否启用。

        public int MainLightShadowResolution => Mathf.Clamp(mainLightShadowResolution, 16, 8192); // 暴露经过保护的阴影分辨率，避免误填 0 或过大的值导致 RT 创建风险。

        public float MainLightShadowDistance => Mathf.Max(0f, mainLightShadowDistance); // 暴露非负的阴影剔除距离，供相机 culling 阶段决定哪些投影物进入 shadow caster 集合。

        public float MainLightShadowDepthBias => Mathf.Max(0f, mainLightShadowDepthBias); // 暴露非负的常量深度偏移，供 ShadowCaster Pass 设置 GPU 深度偏移。

        public float MainLightShadowNormalBias => Mathf.Max(0f, mainLightShadowNormalBias); // 暴露非负的顶点 normal bias 倍率，供 ShadowCaster shader 抑制倾斜表面的 acne。

        public float MainLightShadowSampleBias => Mathf.Max(0f, mainLightShadowSampleBias); // 暴露非负的接收端采样偏移，供 Lit shader 在比较 shadow map 前使用。

        public bool EnableMainLightShadowDebugView => enableMainLightShadowDebugView; // 暴露 shadow map 可视化开关给 Graph Assembler 使用。

        public float MainLightShadowDebugExposure => Mathf.Max(0.0001f, mainLightShadowDebugExposure); // 暴露经过保护的 shadow map 调试亮度，避免 shader 收到无效倍率。

        public BurtShadowDebugYFlipMode MainLightShadowDebugYFlipMode => mainLightShadowDebugYFlipMode; // 暴露 shadow map 调试图的翻转模式，让 Debug Pass 可以按 Inspector 配置解析方向。

        public bool EnableMainLightShadowDebugLog => enableMainLightShadowDebugLog; // 暴露主光阴影诊断日志开关，所有日志输出都必须先检查它。

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

        private bool IsDeferredRendererMode => rendererMode == BurtRendererMode.Deferred; // 提供给 Odin ShowIf 使用，让 Deferred 专属配置只在 Deferred 模式下显示。

        private BurtPostProcessSettings EnsurePostProcessSettings() // 定义后处理设置兜底函数，避免旧资产还没有序列化新字段时返回空引用。
        {
            if (postProcessSettings == null) // 如果 Unity 还没有给旧资产创建后处理设置对象，就在访问时补一个默认实例。
            {
                postProcessSettings = new BurtPostProcessSettings(); // 创建默认后处理设置，默认关闭后处理框架以保持旧画面不变。
            }

            return postProcessSettings; // 返回可用的后处理设置对象，供外部只读访问。
        }

        protected override void OnValidate() // 重写 RenderPipelineAsset 的 OnValidate，避免隐藏基类同名函数产生 CS0114 警告。
        {
            base.OnValidate(); // 先执行 Unity 管线资产自己的校验逻辑，保持 RenderPipelineAsset 内部刷新行为不丢失。
            EnsurePostProcessSettings(); // 确保后处理设置对象存在，避免旧资产在 Inspector 中显示为空。
        }

        protected override UnityEngine.Rendering.RenderPipeline CreatePipeline() // Unity 会调用这个函数来创建真正运行时的 RenderPipeline 实例。
        {
            return new BurtRenderPipeline(this); // 创建 BurtRenderPipeline，并把当前资产传进去作为配置来源。
        }
    }
}
