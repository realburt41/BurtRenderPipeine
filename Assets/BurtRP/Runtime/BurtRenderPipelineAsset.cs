using UnityEngine; // 引入 UnityEngine 命名空间，用来使用 Color、SerializeField、CreateAssetMenu 等 Unity 类型。
using UnityEngine.Rendering; // 引入 Unity 渲染命名空间，用来继承 RenderPipelineAsset。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让管线资产和其他 BurtRP 代码处在同一个模块里。
{
    [CreateAssetMenu(menuName = "Rendering/Burt Render Pipeline Asset", fileName = "BurtRenderPipelineAsset")] // 让 Unity 可以通过 Create 菜单创建 BurtRenderPipelineAsset。
    public sealed class BurtRenderPipelineAsset : RenderPipelineAsset // 定义 BurtRP 的管线资产，Unity Graphics Settings 会引用它来创建管线实例。
    {
        [SerializeField] private Color clearColor = new Color(0.02f, 0.02f, 0.025f, 1f); // 定义默认清屏颜色，并暴露到 Inspector 供你调整。

        [SerializeField] private bool enableDepthPrepass = true; // 定义是否启用 Depth Prepass，默认开启，方便当前阶段观察深度预写流程。

        [SerializeField] private bool enableDepthDebugView = false; // 定义是否把 CameraDepth 可视化到最终颜色目标，默认关闭以避免覆盖正常画面。

        [SerializeField] private float depthDebugScale = 50f; // 定义深度可视化的亮度缩放，数值越大越容易看清近处深度变化。

        [SerializeField] private bool enableUnsupportedShaderDebug = true; // Enables a debug draw that paints non-BurtRP shaders with Unity error material, so wrong materials are visible.

        [SerializeField] private bool enableRenderGraphDebug = false; // 定义 RenderGraph 调试输出开关，默认关闭，避免每帧刷 Console。

        public Color ClearColor => clearColor; // 暴露默认清屏颜色给渲染 Pass 使用。

        public bool EnableDepthPrepass => enableDepthPrepass; // 暴露 Depth Prepass 开关给 Graph Assembler 使用。

        public bool EnableDepthDebugView => enableDepthDebugView; // 暴露深度可视化开关给 Graph Assembler 使用。

        public float DepthDebugScale => Mathf.Max(0.0001f, depthDebugScale); // 暴露经过保护的深度可视化缩放，避免 shader 收到 0 或负数。

        public bool EnableUnsupportedShaderDebug => enableUnsupportedShaderDebug; // Exposes the unsupported-shader debug switch to the graph assembler.

        public bool EnableRenderGraphDebug => enableRenderGraphDebug; // 暴露 RenderGraph 调试开关给 BurtCameraRenderer 使用。

        protected override UnityEngine.Rendering.RenderPipeline CreatePipeline() // Unity 会调用这个函数来创建真正运行时的 RenderPipeline 实例。
        {
            return new BurtRenderPipeline(this); // 创建 BurtRenderPipeline，并把当前资产传进去作为配置来源。
        }
    }
}
