using UnityEngine; // 引入 UnityEngine 命名空间，用来读取 Camera、CameraClearFlags 和 Color。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让清屏工具可以被运行时 Pass 直接访问。
{
    internal static class BurtCameraClearUtility // 定义相机清屏工具类，集中处理 BurtCameraData 和 Unity 编辑器相机的清屏规则。
    {
        public static BurtCameraClearMode ResolveClearMode(BurtRenderRequest request) // 根据当前 request 解析最终清屏模式。
        {
            if (request == null) // 如果 request 为空，说明调用方没有有效相机任务。
            {
                return BurtCameraClearMode.SolidColor; // 使用纯色清屏作为安全兜底，避免未知 request 直接继承旧画面。
            }

            var cameraData = request.CameraData; // 从 request 中读取 BurtCameraData，普通 Game 相机通常通过它控制清屏模式。

            if (cameraData != null) // 如果相机挂了 BurtCameraData，就以 BurtRP 自己的配置为最高优先级。
            {
                return cameraData.ClearMode; // 返回 Inspector 上配置的 BurtRP 清屏模式。
            }

            if (ShouldFollowUnityCameraClearFlags(request)) // 如果是 SceneView/Preview 这类没有 BurtCameraData 的编辑器相机，就需要尊重 Unity 自己的 clearFlags。
            {
                return ResolveUnityClearMode(request.Camera); // 把 Unity CameraClearFlags 转换为 BurtRP 的清屏枚举。
            }

            return BurtCameraClearMode.SolidColor; // 没有 BurtCameraData 的普通运行时相机继续保持旧行为，默认按管线颜色清屏。
        }

        public static Color ResolveClearColor( // 根据 request、asset 和清屏模式解析最终清屏颜色。
            BurtRenderRequest request, // 接收当前渲染请求，用来读取相机和 BurtCameraData。
            BurtRenderPipelineAsset asset, // 接收管线资产，用来读取默认清屏色。
            BurtCameraClearMode clearMode) // 接收已经解析好的清屏模式，避免重复计算。
        {
            var cameraData = request != null ? request.CameraData : null; // 安全读取 BurtCameraData，request 为空时得到 null。

            if (cameraData != null) // 如果相机挂了 BurtCameraData，就优先使用相机自己的清屏颜色。
            {
                return cameraData.ClearColor; // 返回 BurtCameraData 上配置的清屏颜色。
            }

            var camera = request != null ? request.Camera : null; // 安全读取 Unity Camera，request 或相机为空时得到 null。

            if (camera != null && ShouldUseCameraBackgroundColor(request, clearMode)) // 如果当前路径应该跟随 Unity 相机背景色，就用 camera.backgroundColor。
            {
                return camera.backgroundColor; // 返回 Unity Camera 的背景色，SceneView/Preview 的纯色或天空盒底色都会走这里。
            }

            if (asset != null) // 如果没有相机专属颜色，但管线资产存在，就使用资产默认颜色。
            {
                return asset.ClearColor; // 返回 BurtRenderPipelineAsset 上的默认清屏颜色。
            }

            return Color.black; // 没有任何配置来源时使用黑色作为最后兜底。
        }

        private static bool ShouldFollowUnityCameraClearFlags(BurtRenderRequest request) // 判断当前 request 是否应该使用 Unity Camera.clearFlags。
        {
            if (request == null) // 空 request 没有角色信息，也没有相机信息。
            {
                return false; // 不跟随 Unity clearFlags，避免异常路径改变旧行为。
            }

            if (request.CameraRole == BurtCameraRole.SceneView) // SceneView 相机来自编辑器窗口，通常没有 BurtCameraData。
            {
                return true; // SceneView 必须尊重 Unity 自己的 Skybox 开关，否则 Scene 窗口不会切到天空盒。
            }

            if (request.CameraRole == BurtCameraRole.Preview) // Preview 相机来自 Inspector 或预览窗口，也通常没有 BurtCameraData。
            {
                return true; // Preview 使用 Unity 自己的 clearFlags，避免预览窗口背景表现异常。
            }

            return false; // 普通 Game/Base/Overlay/UI 相机仍然以 BurtCameraData 为主要配置来源。
        }

        private static BurtCameraClearMode ResolveUnityClearMode(Camera camera) // 把 Unity CameraClearFlags 转换为 BurtRP 的清屏模式。
        {
            if (camera == null) // 如果相机为空，就不能读取 Unity clearFlags。
            {
                return BurtCameraClearMode.SolidColor; // 使用纯色清屏作为安全兜底。
            }

            switch (camera.clearFlags) // 根据 Unity 相机的 clearFlags 决定 BurtRP 清屏语义。
            {
                case CameraClearFlags.Skybox: // Unity 相机要求绘制天空盒。
                    return BurtCameraClearMode.Skybox; // 转换为 BurtRP 的 Skybox 模式。
                case CameraClearFlags.Depth: // Unity 相机只清深度。
                    return BurtCameraClearMode.DepthOnly; // 转换为 BurtRP 的 DepthOnly 模式。
                case CameraClearFlags.Nothing: // Unity 相机完全不清屏。
                    return BurtCameraClearMode.DontClear; // 转换为 BurtRP 的 DontClear 模式。
                default: // 其他情况包含 SolidColor/Color 等纯色清屏模式。
                    return BurtCameraClearMode.SolidColor; // 统一转换为 BurtRP 的 SolidColor 模式。
            }
        }

        private static bool ShouldUseCameraBackgroundColor( // 判断清屏颜色是否应该来自 Unity Camera.backgroundColor。
            BurtRenderRequest request, // 接收当前渲染请求，用来判断是否为编辑器相机路径。
            BurtCameraClearMode clearMode) // 接收已经解析好的清屏模式。
        {
            if (clearMode == BurtCameraClearMode.Skybox) // Skybox 模式通常也需要一个背景底色，给天空盒未覆盖或异常路径兜底。
            {
                return true; // 使用相机背景色作为 Skybox 绘制前的底色。
            }

            if (!ShouldFollowUnityCameraClearFlags(request)) // 如果不是 SceneView/Preview，就不应该偷用 Unity Camera 的背景色覆盖 BurtRP 资产配置。
            {
                return false; // 普通运行时相机继续使用 BurtCameraData 或 Asset 的颜色。
            }

            return clearMode == BurtCameraClearMode.SolidColor; // SceneView/Preview 的纯色模式应该跟随 Unity 自己的背景色。
        }
    }
}
