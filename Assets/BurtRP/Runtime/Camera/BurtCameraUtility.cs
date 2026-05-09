// 引入 UnityEngine 命名空间，用来访问 Camera、CameraType 等 Unity 相机类型。
using UnityEngine;

// 定义 Burt 自己的渲染管线命名空间，保证工具类和 request、pipeline 位于同一个模块。
namespace Burt.RenderPipeline
{
    // 集中处理“Unity 相机应该被 BurtRP 归类成哪种渲染请求”的规则，避免 BurtRenderRequest 继续承担分类细节。
    internal static class BurtCameraUtility
    {
        // 根据 Unity Camera 和可选的 BurtCameraData 推导 BurtRP 的相机栈角色。
        public static BurtCameraRole ResolveCameraRole(Camera camera, BurtCameraData cameraData)
        {
            // 如果传入的相机为空，说明调用方没有可分类的对象，使用 Base 作为安全兜底。
            if (camera == null)
            {
                // 返回基础相机角色，避免上层排序和调试遇到空枚举状态。
                return BurtCameraRole.Base;
            }

            // Unity 的 Preview 相机通常来自材质球预览、Inspector 预览或编辑器内部预览，必须优先于手动角色覆盖。
            if (camera.cameraType == CameraType.Preview)
            {
                // 返回 Preview 角色，保持现有预览相机路径不变。
                return BurtCameraRole.Preview;
            }

            // SceneView 来自编辑器视图，优先按编辑器相机处理，避免误归类到 GameView 的 Base 栈。
            if (camera.cameraType == CameraType.SceneView)
            {
                // 返回 SceneView 角色，方便调试日志和排序区分编辑器视图。
                return BurtCameraRole.SceneView;
            }

            // 项目相机挂了 BurtCameraData 时，以显式配置的 CameraRole 作为分类来源。
            if (cameraData != null)
            {
                // 返回 Inspector 上配置的角色，支持 Base、Overlay、UI 三类运行时相机。
                return cameraData.CameraRole;
            }

            // 没有 BurtCameraData 的普通相机继续作为 Base 渲染，避免改变现有 Forward 渲染结果。
            return BurtCameraRole.Base;
        }

        // 根据已经解析好的相机角色推导 BurtRP 的 request 类型。
        public static BurtRenderRequestType ResolveRequestType(BurtCameraRole cameraRole)
        {
            // 用角色到 request 类型的一对一映射承载第一版 Camera Stack 分类。
            switch (cameraRole)
            {
                case BurtCameraRole.Overlay:
                    return BurtRenderRequestType.OverlayCamera;
                case BurtCameraRole.UI:
                    return BurtRenderRequestType.UICamera;
                case BurtCameraRole.SceneView:
                    return BurtRenderRequestType.SceneView;
                case BurtCameraRole.Preview:
                    return BurtRenderRequestType.Preview;
                case BurtCameraRole.Base:
                default:
                    return BurtRenderRequestType.BaseCamera;
            }
        }
    }
}
