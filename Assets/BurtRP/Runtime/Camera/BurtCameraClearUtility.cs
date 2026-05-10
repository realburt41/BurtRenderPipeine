using UnityEngine; // 引入 UnityEngine 命名空间，用来读取 Camera、CameraClearFlags 和 Color。

#if UNITY_EDITOR // 只在 Unity 编辑器环境下启用 UnityEditor API，避免 Player 构建引用编辑器程序集。
using UnityEditor; // 引入 UnityEditor 命名空间，用来读取当前 Inspector 选中的对象，给 SceneView 和 Camera Preview 找到源相机配置。
#endif // 结束编辑器专用引用区域。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让清屏工具可以被运行时 Pass 直接访问。
{
    internal static class BurtCameraClearUtility // 定义相机清屏工具类，集中处理 BurtCameraData 和 Unity 编辑器相机的清屏规则。
    {
#if UNITY_EDITOR // 只在编辑器里缓存最近明确选中的 BurtCameraData，Player 构建不需要这个状态。
        private static BurtCameraData editorRegisteredCameraData; // 缓存最近由 BurtCameraData 主动登记的相机数据，作为 Selection 读取失败时的兜底来源。
#endif // 结束编辑器缓存字段。

        public static BurtCameraClearMode ResolveClearMode(BurtRenderRequest request) // 根据当前 request 解析最终清屏模式。
        {
            if (request == null) // 如果 request 为空，说明调用方没有有效相机任务。
            {
                return BurtCameraClearMode.SolidColor; // 使用纯色清屏作为安全兜底，避免未知 request 直接继承旧画面。
            }

            var cameraData = ResolveClearCameraData(request); // 解析真正用于清屏的 BurtCameraData，SceneView/Preview 在编辑器下可以借用当前选中相机的数据。

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
            var cameraData = ResolveClearCameraData(request); // 安全解析清屏数据来源，request 为空或没有可用数据时得到 null。

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

        public static BurtCameraData ResolveClearCameraData(BurtRenderRequest request) // 解析本次清屏应该使用哪份 BurtCameraData。
        {
            if (request == null) // 如果 request 为空，说明没有渲染任务可供解析。
            {
                return null; // 返回 null，让调用方继续走 Unity clearFlags 或资产默认值。
            }

            if (request.CameraData != null) // 如果当前 request 自己就有关联 BurtCameraData，这是最直接、最可靠的数据来源。
            {
                return request.CameraData; // 直接返回 request 自己的 BurtCameraData。
            }

#if UNITY_EDITOR // 下面这段只服务编辑器里的 SceneView 和 Camera Preview，不会进入 Player 构建。
            var editorSelectedCameraData = ResolveEditorSelectedCameraData(request); // 尝试给 SceneView/Preview 找到 Inspector 当前选中的源相机数据。

            if (editorSelectedCameraData != null) // 如果找到了源相机的 BurtCameraData，说明编辑器相机可以复用它的清屏设置。
            {
                return editorSelectedCameraData; // 返回源相机数据，让 SceneView/Preview 跟随选中 Camera 的 Burt 清屏设置。
            }
#endif // 结束编辑器专用 SceneView/Preview 源相机解析。

            return null; // 没有 BurtCameraData 时返回 null，让后续逻辑使用 Unity 原生 clearFlags 或管线默认值。
        }

        public static string ResolveClearDataSourceName(BurtRenderRequest request) // 返回清屏数据来源名称，专门给调试日志使用。
        {
            if (request == null) // 如果 request 为空，说明没有明确的数据来源。
            {
                return "Fallback"; // 返回兜底来源名称，方便日志识别异常路径。
            }

            if (request.CameraData != null) // 当前 request 直接拥有 BurtCameraData 时，这是最明确的数据来源。
            {
                return "BurtCameraData"; // 返回直接 BurtCameraData 来源名称。
            }

#if UNITY_EDITOR // 下面这段只在编辑器里检查 SceneView/Preview 的源相机。
            if (ResolveEditorSelectedCameraData(request) != null) // 如果编辑器相机能从当前选中 Camera 找到 BurtCameraData，就标记成编辑器选中来源。
            {
                return "EditorSelectedCamera"; // 返回编辑器选中相机来源名称，方便判断 SceneView/Preview 是否正在借用源相机配置。
            }
#endif // 结束编辑器专用来源判断。

            if (ShouldFollowUnityCameraClearFlags(request)) // SceneView/Preview 等编辑器相机没有 Burt 数据时，会跟随 Unity 原生 clearFlags。
            {
                return "UnityCamera"; // 返回 Unity Camera 来源名称，表示 clearFlags/backgroundColor 是当前依据。
            }

            return "PipelineFallback"; // 普通运行时相机没有 BurtCameraData 时，会走管线默认兜底。
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

#if UNITY_EDITOR // 只在 Unity 编辑器中编译编辑器相机源数据查找逻辑，Player 构建不包含 UnityEditor 依赖。
        public static void RegisterEditorClearCameraData(BurtCameraData cameraData) // 由 BurtCameraData 主动登记自己，避免 SceneView 渲染时 Selection 临时读不到源相机。
        {
            if (cameraData == null) // 如果传入的数据为空，说明没有有效相机可以缓存。
            {
                return; // 直接结束，保留原来的缓存，避免空对象覆盖有效对象。
            }

            editorRegisteredCameraData = cameraData; // 缓存最近明确操作过的 BurtCameraData，供 SceneView/Preview 兜底使用。
        }

        private static BurtCameraData ResolveEditorSelectedCameraData(BurtRenderRequest request) // 给 SceneView/Preview 查找 Inspector 当前选中相机上的 BurtCameraData。
        {
            if (request == null) // 如果 request 为空，就没有相机角色可判断。
            {
                return null; // 返回 null，让调用方继续走普通清屏路径。
            }

            var isSceneView = request.CameraRole == BurtCameraRole.SceneView; // 判断当前 request 是否来自 Unity 编辑器 SceneView。

            var isPreview = request.CameraRole == BurtCameraRole.Preview; // 判断当前 request 是否来自 Unity 编辑器 Preview Camera。

            if (!isSceneView && !isPreview) // 只有 Unity 的 SceneView/Preview 相机需要借用选中相机配置。
            {
                return null; // 非编辑器辅助相机不能偷用 Selection，避免 GameView 被选中对象意外影响。
            }

            var selectedCameraData = ResolveCameraDataFromEditorSelection(); // 从 Unity 当前选中对象解析 BurtCameraData，兼容选中 GameObject、Camera 组件或 BurtCameraData 组件的情况。

            if (selectedCameraData == null) // 如果 Selection 当前没有解析到 BurtCameraData，就尝试使用主动登记的缓存。
            {
                selectedCameraData = editorRegisteredCameraData; // 使用最近被 BurtCameraData.OnValidate 或选中同步流程登记的相机数据。
            }

            if (selectedCameraData == null) // 如果当前选中对象无法解析到 BurtCameraData，编辑器相机没有可借用的源相机。
            {
                return null; // 返回 null，让 SceneView/Preview 回退到自己的 Unity clearFlags。
            }

            if (!selectedCameraData.EnableRender) // 如果源相机在 BurtRP 中被禁用渲染，就不应该让 SceneView/Preview 继续使用它的配置。
            {
                return null; // 返回 null，让 SceneView/Preview 使用自己的 Unity 原生配置。
            }

            return selectedCameraData; // 返回当前选中相机的数据，让 SceneView/Preview 跟随它的 Skybox/SolidColor 设置。
        }

        private static BurtCameraData ResolveCameraDataFromEditorSelection() // 从 Unity 当前选择对象解析 BurtCameraData。
        {
            var selectedObject = Selection.activeObject; // 读取 Inspector 当前激活对象，它可能是 GameObject、Camera 组件、BurtCameraData 组件或资源对象。

            var cameraData = ResolveCameraDataFromObject(selectedObject); // 优先从 activeObject 解析，避免只选中组件时 activeGameObject 信息不完整。

            if (cameraData != null) // 如果 activeObject 已经能解析到 BurtCameraData，就不需要继续查 activeGameObject。
            {
                return cameraData; // 返回解析到的 BurtCameraData。
            }

            var selectedGameObject = Selection.activeGameObject; // 读取当前激活 GameObject，作为 activeObject 不是场景组件时的补充来源。

            return ResolveCameraDataFromObject(selectedGameObject); // 从 activeGameObject 再尝试解析一次，失败时自然返回 null。
        }

        private static BurtCameraData ResolveCameraDataFromObject(UnityEngine.Object selectedObject) // 从任意 Unity 选择对象上尝试提取 BurtCameraData。
        {
            if (selectedObject == null) // 如果没有选择对象，就没有数据可解析。
            {
                return null; // 返回 null 表示没有找到 BurtCameraData。
            }

            var selectedCameraData = selectedObject as BurtCameraData; // 尝试把选择对象直接当作 BurtCameraData 组件。

            if (selectedCameraData != null) // 如果当前选中的就是 BurtCameraData 组件，就已经找到目标数据。
            {
                return selectedCameraData; // 返回直接选中的 BurtCameraData。
            }

            var selectedCamera = selectedObject as Camera; // 尝试把选择对象当作 Unity Camera 组件。

            if (selectedCamera != null) // 如果当前选中的是 Camera 组件，就从同一个 GameObject 上找 BurtCameraData。
            {
                selectedCamera.TryGetComponent(out BurtCameraData cameraDataFromCamera); // 在 Camera 所在对象上查找 BurtCameraData。

                return cameraDataFromCamera; // 返回查到的数据，查不到时返回 null。
            }

            var selectedGameObject = selectedObject as GameObject; // 尝试把选择对象当作 GameObject。

            if (selectedGameObject != null) // 如果当前选中的是 GameObject，就直接从对象上找 BurtCameraData。
            {
                selectedGameObject.TryGetComponent(out BurtCameraData cameraDataFromGameObject); // 在 GameObject 上查找 BurtCameraData。

                return cameraDataFromGameObject; // 返回查到的数据，查不到时返回 null。
            }

            var selectedComponent = selectedObject as Component; // 尝试把选择对象当作普通组件，用来覆盖 Inspector 选中 Transform 或其他组件的情况。

            if (selectedComponent != null) // 如果当前选中的是某个组件，就从组件所在 GameObject 上找 BurtCameraData。
            {
                selectedComponent.TryGetComponent(out BurtCameraData cameraDataFromComponent); // 在组件所在对象上查找 BurtCameraData。

                return cameraDataFromComponent; // 返回查到的数据，查不到时返回 null。
            }

            return null; // 选择对象不是场景组件或 GameObject 时，无法作为编辑器相机清屏来源。
        }
#endif // 结束编辑器专用 SceneView/Preview 源相机查找逻辑。
    }
}
