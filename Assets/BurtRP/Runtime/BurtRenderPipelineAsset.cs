using UnityEngine; // 引入 UnityEngine 命名空间，用来使用 Color、SerializeField、CreateAssetMenu 等 Unity 类型。
using UnityEngine.Rendering; // 引入 Unity 渲染命名空间，用来继承 RenderPipelineAsset。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让管线资产和其他 BurtRP 代码处在同一个模块里。
{
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
        [SerializeField] private Color clearColor = new Color(0.02f, 0.02f, 0.025f, 1f); // 定义默认清屏颜色，并暴露到 Inspector 供你调整。

        [SerializeField] private bool enableDepthPrepass = true; // 定义是否启用 Depth Prepass，默认开启，方便当前阶段观察深度预写流程。

        [SerializeField] private bool enableDepthDebugView = false; // 定义是否把 CameraDepth 可视化到最终颜色目标，默认关闭以避免覆盖正常画面。

        [SerializeField] private float depthDebugScale = 50f; // 定义深度可视化的亮度缩放，数值越大越容易看清近处深度变化。

        [Header("PBR / Shading")] // 把 PBR 共享查找表集中显示，方便确认 BRDF 使用的全局资源。
        [SerializeField] private Texture2D preintegratedFGLut; // 保存预积分 FG LUT，默认指向 Assets/Textures/PreintegratedFG.exr。

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

        [SerializeField] private bool enableUnsupportedShaderDebug = true; // Enables a debug draw that paints non-BurtRP shaders with Unity error material, so wrong materials are visible.

        [SerializeField] private bool enableRenderGraphDebug = false; // 定义 RenderGraph 调试输出开关，默认关闭，避免每帧刷 Console。

        [Header("Camera Debug")] // 把相机相关调试开关单独分组，避免和阴影、深度等其他模块混在一起。
        [SerializeField] private bool enableCameraSortDebugLog = false; // 定义是否输出相机 request 排序列表，默认关闭，避免每帧多相机时刷 Console。
        [SerializeField] private bool enableRenderFrameDebugLog = false; // 定义是否输出 Frame/Stack 分组日志，默认关闭，避免每帧打印相机栈诊断。

        public Color ClearColor => clearColor; // 暴露默认清屏颜色给渲染 Pass 使用。

        public bool EnableDepthPrepass => enableDepthPrepass; // 暴露 Depth Prepass 开关给 Graph Assembler 使用。

        public bool EnableDepthDebugView => enableDepthDebugView; // 暴露深度可视化开关给 Graph Assembler 使用。

        public float DepthDebugScale => Mathf.Max(0.0001f, depthDebugScale); // 暴露经过保护的深度可视化缩放，避免 shader 收到 0 或负数。

        public Texture2D PreintegratedFGLut => preintegratedFGLut; // 暴露预积分 FG LUT，RenderPipeline 会把它绑定成全局 shader 纹理。

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

        public bool EnableUnsupportedShaderDebug => enableUnsupportedShaderDebug; // Exposes the unsupported-shader debug switch to the graph assembler.

        public bool EnableRenderGraphDebug => enableRenderGraphDebug; // 暴露 RenderGraph 调试开关给 BurtCameraRenderer 使用。

        public bool EnableCameraSortDebugLog => enableCameraSortDebugLog; // 暴露相机排序调试开关给 BurtRenderPipeline 使用，只有打开时才会输出每帧 request 列表。

        public bool EnableRenderFrameDebugLog => enableRenderFrameDebugLog; // 暴露 Frame/Stack 分组调试开关给 BurtRenderPipeline 使用，只诊断分组不改变渲染结果。

        protected override UnityEngine.Rendering.RenderPipeline CreatePipeline() // Unity 会调用这个函数来创建真正运行时的 RenderPipeline 实例。
        {
            return new BurtRenderPipeline(this); // 创建 BurtRenderPipeline，并把当前资产传进去作为配置来源。
        }
    }
}
