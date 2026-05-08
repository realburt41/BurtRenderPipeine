using UnityEngine; // 引入 UnityEngine 命名空间，用来使用 Color、SerializeField、CreateAssetMenu 等 Unity 类型。
using UnityEngine.Rendering; // 引入 Unity 渲染命名空间，用来继承 RenderPipelineAsset。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让管线资产和其他 BurtRP 代码处在同一个模块里。
{
    [CreateAssetMenu(menuName = "Rendering/Burt Render Pipeline Asset", fileName = "BurtRenderPipelineAsset")] // 让 Unity 可以通过 Create 菜单创建 BurtRenderPipelineAsset。
    public sealed class BurtRenderPipelineAsset : RenderPipelineAsset // 定义 BurtRP 的管线资产，Unity Graphics Settings 会引用它来创建管线实例。
    {
        [SerializeField] private Color clearColor = new Color(0.02f, 0.02f, 0.025f, 1f); // 定义默认清屏颜色，并暴露到 Inspector 供你调整。

        [SerializeField] private bool enableRenderGraphDebug = false; // 定义 RenderGraph 调试输出开关，默认关闭，避免每帧刷 Console。

        public Color ClearColor => clearColor; // 暴露默认清屏颜色给渲染 Pass 使用。

        public bool EnableRenderGraphDebug => enableRenderGraphDebug; // 暴露 RenderGraph 调试开关给 BurtCameraRenderer 使用。

        protected override UnityEngine.Rendering.RenderPipeline CreatePipeline() // Unity 会调用这个函数来创建真正运行时的 RenderPipeline 实例。
        {
            return new BurtRenderPipeline(this); // 创建 BurtRenderPipeline，并把当前资产传进去作为配置来源。
        }
    }
}
