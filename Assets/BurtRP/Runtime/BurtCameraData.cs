// 引入 UnityEngine 命名空间，用来使用 MonoBehaviour、Camera、Color、SerializeField 等 Unity 类型。
using UnityEngine;

// 定义 Burt 自己的渲染管线命名空间，和其他 BurtRP 运行时代码保持一致。
namespace Burt.RenderPipeline
{
    // 定义 BurtRP 第一版相机栈角色；当前只用于 request 分类和排序，不负责真正的合成。
    public enum BurtCameraRole
    {
        // 基础场景相机，作为一个相机栈的起点。
        Base = 0,

        // 叠加相机，预留给角色描边、小地图或特殊层叠加。
        Overlay = 1,

        // UI 相机，预留给后续 UI 渲染或合成阶段。
        UI = 2,

        // Unity 编辑器 SceneView 相机，和 GameView 相机分开标记。
        SceneView = 3,

        // Unity 预览相机，通常来自 Inspector 或材质预览。
        Preview = 4,

        // Unity 反射探针捕获相机，通常来自 ReflectionProbe 烘焙、实时刷新或相关预览。
        Reflection = 5
    }

    // 定义 BurtRP 自己的相机清屏模式。
    public enum BurtCameraClearMode
    {
        // 使用天空盒模式：先清深度，再绘制天空盒作为背景。
        Skybox = 0,

        // 使用纯色清屏模式：清深度并用指定颜色清颜色缓冲。
        SolidColor = 1,

        // 只清深度模式：保留颜色缓冲，只清理深度缓冲。
        DepthOnly = 2,

        // 不清屏模式：不清颜色也不清深度，通常只用于特殊叠加相机。
        DontClear = 3
    }

    public enum BurtCameraAntialiasingMode
    {
        None = 0,

        [InspectorName("Temporal Anti-aliasing (TAA)")]
        TemporalAntialiasing = 1
    }

    // 限制同一个 GameObject 上只能挂一个 BurtCameraData。
    [DisallowMultipleComponent]

    // 要求挂 BurtCameraData 的 GameObject 必须同时有 Camera 组件。
    [RequireComponent(typeof(Camera))]

    // 让这个组件在编辑器非播放状态也能执行 Unity 生命周期，方便编辑器里实时同步 Camera.depth 和 Camera 清屏字段。
    [ExecuteAlways]

    // 定义 BurtRP 的相机扩展数据组件。
    public sealed class BurtCameraData : MonoBehaviour
    {
        // 控制这个相机是否参与 BurtRP 渲染。
        [SerializeField] private bool enableRender = true;

        // 控制这个相机在多相机情况下的渲染顺序，数值越小越先渲染。
        [SerializeField] private int renderOrder = 0;

        // 声明这个相机在 BurtRP 相机栈里的角色；当前会影响 request 分类、同层排序和 Overlay 清屏策略，不触碰 PBR/Shader。
        [SerializeField] private BurtCameraRole cameraRole = BurtCameraRole.Base;

        // 声明这个相机属于哪个逻辑栈；Base/Overlay/UI 可以用同一个 stackId 表示后续应合成到同一组。
        [SerializeField] private int stackId = 0;

        // 声明 Overlay 相机是否希望清理颜色；默认不清颜色，让 Overlay 可以叠加在 Base 输出之上。
        [SerializeField] private bool overlayClearsColor = false;

        // 声明 Overlay 相机是否希望清理深度；默认清深度，避免 Base 深度挡住 Overlay 物体。
        [SerializeField] private bool overlayClearsDepth = true;

        // 控制是否把 BurtRP 的 renderOrder 自动同步到 Unity 原生 Camera.depth；这只是为了让 Unity 原生相机面板和外部工具看到一致深度，不作为 BurtRP 排序的唯一来源。
        [SerializeField] private bool syncRenderOrderToCameraDepth = true;

        // 控制这个相机渲染前如何清理颜色和深度缓冲。
        [SerializeField] private BurtCameraClearMode clearMode = BurtCameraClearMode.Skybox;

        // 控制 SolidColor 模式下使用的清屏颜色。
        [SerializeField] private Color clearColor = new(0.02f, 0.02f, 0.025f, 1f);

        [SerializeField] private BurtCameraAntialiasingMode antialiasingMode = BurtCameraAntialiasingMode.None;

        // 控制是否把 BurtRP 的清屏模式和清屏颜色反向同步到 Unity 原生 Camera 组件，方便 SceneView、Camera Preview 和第三方工具看到一致配置。
        [SerializeField] private bool syncClearSettingsToUnityCamera = true;

        // 缓存当前 GameObject 上的 Camera 组件，避免每帧反复 GetComponent。
        private Camera cachedCamera;

        // 记录上一次已经同步到 Camera.depth 的 renderOrder，用来判断是否需要重新同步。
        private int lastSyncedRenderOrder = int.MinValue;

        // 记录上一次已经同步到 Unity Camera.clearFlags 的 BurtRP 清屏模式，避免每帧重复写原生 Camera 字段。
        private BurtCameraClearMode lastSyncedClearMode = (BurtCameraClearMode)(-1);

        // 记录上一次已经同步到 Unity Camera.backgroundColor 的 BurtRP 清屏颜色，避免每帧重复写原生 Camera 字段。
        private Color lastSyncedClearColor = new(-1f, -1f, -1f, -1f);

        // 暴露只读属性，让渲染器可以读取 enableRender，但外部不能随意改字段。
        public bool EnableRender => enableRender;

        // 暴露只读属性，让渲染器可以在每帧创建 request 时直接读取 renderOrder，用于 BurtRP 自己的相机排序。
        public int RenderOrder => renderOrder;

        // 暴露只读属性，让 request 分类逻辑可以读取相机栈角色。
        public BurtCameraRole CameraRole => cameraRole;

        // 暴露只读属性，让排序和调试逻辑可以读取逻辑栈编号。
        public int StackId => stackId;

        // 暴露只读属性，记录 Overlay 相机是否希望清颜色，供 request 和清屏 Pass 使用。
        public bool OverlayClearsColor => overlayClearsColor;

        // 暴露只读属性，记录 Overlay 相机是否希望清深度，供 request 和清屏 Pass 使用。
        public bool OverlayClearsDepth => overlayClearsDepth;

        // 暴露只读属性，让渲染器可以读取 clearMode，用于决定清屏策略。
        public BurtCameraClearMode ClearMode => clearMode;

        // 暴露只读属性，让渲染器可以读取 clearColor，用于纯色清屏。
        public Color ClearColor => clearColor;

        public BurtCameraAntialiasingMode AntialiasingMode => antialiasingMode;

        // Unity 在组件启用时调用这个函数，适合初始化缓存和同步相机深度。
        private void OnEnable()
        {
            // 获取并缓存当前 GameObject 上的 Camera 组件。
            CacheCamera();

            // 立刻把当前 renderOrder 同步到 Camera.depth。
            SyncRenderOrderToCameraDepth(forceSync: true);

            // 立刻把当前清屏模式和清屏颜色同步到 Unity Camera 组件，保证启用后 Inspector 原生字段马上对齐 BurtCameraData。
            SyncClearSettingsToUnityCamera(forceSync: true);
        }

        // Unity 在 Inspector 修改字段时调用这个函数，适合让编辑器里的改动立即生效。
        private void OnValidate()
        {
            // 获取并缓存当前 GameObject 上的 Camera 组件。
            CacheCamera();

            // Inspector 改值后强制同步一次，避免要等启用/禁用相机才刷新。
            SyncRenderOrderToCameraDepth(forceSync: true);

            // Inspector 改清屏模式或清屏颜色后强制同步一次，保证 Camera.clearFlags 和 Camera.backgroundColor 立即刷新。
            SyncClearSettingsToUnityCamera(forceSync: true);

#if UNITY_EDITOR
            // OnValidate 只会针对当前被修改的组件触发，所以这里直接登记当前相机作为编辑器清屏参考来源。
            RegisterEditorClearSource();

            // Inspector 修改 BurtCameraData 后主动刷新 SceneView，避免 SceneView 没重绘时还显示旧背景。
            RequestEditorViewRepaint();
#endif
        }

        // Unity 每帧调用这个函数；因为有 ExecuteAlways，编辑器非播放状态也可能调用。
        private void Update()
        {
            // 每帧检查 renderOrder 是否变化，变化时同步到 Camera.depth；BurtRP 排序仍然会在创建 request 时直接读取 RenderOrder。
            SyncRenderOrderToCameraDepth(forceSync: false);

            // 每帧检查清屏配置是否变化，变化时同步到 Unity Camera；这样编辑器滑动或脚本改值都能及时反映到原生相机组件。
            SyncClearSettingsToUnityCamera(forceSync: false);

#if UNITY_EDITOR
            // 编辑器下如果当前相机正被选中，就持续登记它，避免 SceneView 渲染时 Selection 状态短暂不可读。
            RegisterEditorClearSourceIfSelected();
#endif
        }

        // 缓存 Camera 组件的辅助函数。
        private void CacheCamera()
        {
            // 如果缓存里已经有 Camera，就不需要重复获取。
            if (cachedCamera != null)
            {
                // 直接结束函数。
                return;
            }

            // 尝试从当前 GameObject 获取 Camera 组件，并保存到 cachedCamera。
            cachedCamera = GetComponent<Camera>();
        }

        // 把 BurtRP 的 renderOrder 同步到 Unity 原生 Camera.depth；这个函数只负责对齐 Unity 原生字段，排序权威来源在 BurtCameraSortUtility.ResolveSortLayer。
        private void SyncRenderOrderToCameraDepth(bool forceSync)
        {
            // 如果用户不希望同步到 Camera.depth，就直接跳过。
            if (!syncRenderOrderToCameraDepth)
            {
                // 结束同步函数。
                return;
            }

            // 确保 Camera 已经被缓存。
            CacheCamera();

            // 如果当前 GameObject 上没有 Camera，就无法同步。
            if (cachedCamera == null)
            {
                // 结束同步函数。
                return;
            }

            // 如果不是强制同步，并且 renderOrder 没有变化，就不重复写 Camera.depth。
            if (!forceSync && lastSyncedRenderOrder == renderOrder)
            {
                // 结束同步函数。
                return;
            }

            // 把 BurtRP 的整数 renderOrder 写到 Unity 原生 Camera.depth。
            cachedCamera.depth = renderOrder;

            // 记录这次已经同步过的 renderOrder。
            lastSyncedRenderOrder = renderOrder;
        }

        // 把 BurtRP 的 clearMode 和 clearColor 同步到 Unity 原生 Camera 组件；渲染仍以 BurtCameraData 为权威来源，这里只做编辑器和外部工具可见性对齐。
        private void SyncClearSettingsToUnityCamera(bool forceSync)
        {
            // 如果用户不希望 BurtCameraData 反写 Unity Camera 组件，就直接跳过。
            if (!syncClearSettingsToUnityCamera)
            {
                // 结束同步函数。
                return;
            }

            // 确保 Camera 已经被缓存。
            CacheCamera();

            // 如果当前 GameObject 上没有 Camera，就无法同步。
            if (cachedCamera == null)
            {
                // 结束同步函数。
                return;
            }

            // 先把 BurtRP 清屏模式转换成 Unity Camera 能显示的 clearFlags，后面比较和写入都复用同一个结果。
            var desiredClearFlags = ConvertToUnityClearFlags(clearMode);

            // 如果不是强制同步，并且 BurtCameraData、Unity Camera.clearFlags、Unity Camera.backgroundColor 三者都已经一致，就不重复写 Unity Camera 字段。
            if (!forceSync && lastSyncedClearMode == clearMode && lastSyncedClearColor == clearColor && cachedCamera.clearFlags == desiredClearFlags && cachedCamera.backgroundColor == clearColor)
            {
                // 结束同步函数。
                return;
            }

            // 把 BurtRP 清屏模式写入 Unity Camera.clearFlags，保证原生 Camera 组件面板跟 BurtCameraData 对齐。
            cachedCamera.clearFlags = desiredClearFlags;

            // 把 BurtRP 清屏颜色同步到 Unity Camera 的背景色；Skybox 模式下它也会作为天空盒前的底色和异常兜底色。
            cachedCamera.backgroundColor = clearColor;

            // 记录这次已经同步过的清屏模式。
            lastSyncedClearMode = clearMode;

            // 记录这次已经同步过的清屏颜色。
            lastSyncedClearColor = clearColor;

#if UNITY_EDITOR
            // 如果当前相机正被选中，就把它登记给 SceneView/Preview 的清屏解析逻辑。
            RegisterEditorClearSourceIfSelected();

            // 如果当前相机正被选中，就把同一套 clearFlags 和背景色直接同步给所有 SceneView 相机。
            SyncSelectedEditorSceneViewsClearSettings(desiredClearFlags, clearColor);

            // 清屏设置真正写入 Unity Camera 后主动刷新 SceneView，让编辑器相机立刻重新走 BurtRP 清屏逻辑。
            RequestEditorViewRepaint();
#endif
        }

        // 把 BurtRP 自己的清屏枚举转换为 Unity 原生 CameraClearFlags，保证 Camera 组件面板和 BurtCameraData 面板语义一致。
        private static CameraClearFlags ConvertToUnityClearFlags(BurtCameraClearMode mode)
        {
            // 根据 BurtRP 清屏模式选择 Unity 原生清屏模式。
            switch (mode)
            {
                // BurtRP Skybox 对应 Unity Camera 的 Skybox。
                case BurtCameraClearMode.Skybox:
                    // 返回 Unity Skybox 清屏模式。
                    return CameraClearFlags.Skybox;

                // BurtRP DepthOnly 对应 Unity Camera 的 Depth。
                case BurtCameraClearMode.DepthOnly:
                    // 返回 Unity 只清深度模式。
                    return CameraClearFlags.Depth;

                // BurtRP DontClear 对应 Unity Camera 的 Nothing。
                case BurtCameraClearMode.DontClear:
                    // 返回 Unity 完全不清屏模式。
                    return CameraClearFlags.Nothing;

                // BurtRP SolidColor 和未知值都按 Unity 纯色清屏处理。
                case BurtCameraClearMode.SolidColor:
                default:
                    // 返回 Unity 纯色清屏模式。
                    return CameraClearFlags.SolidColor;
            }
        }

#if UNITY_EDITOR
        // 判断当前 BurtCameraData 是否就是编辑器里正在操作的对象。
        private bool IsSelectedInEditor()
        {
            // 如果 Unity 当前选中的 GameObject 就是本组件所在对象，说明用户正在操作这台相机。
            if (UnityEditor.Selection.activeGameObject == gameObject)
            {
                // 返回 true，让调用方可以把当前相机作为 SceneView/Preview 的参考相机。
                return true;
            }

            // 如果 Unity 当前选中的对象就是这个 BurtCameraData 组件，说明 Inspector 正在直接编辑它。
            if (UnityEditor.Selection.activeObject == this)
            {
                // 返回 true，让调用方可以登记当前相机数据。
                return true;
            }

            // 确保 cachedCamera 已经准备好，后面要用它判断是否选中了 Camera 组件。
            CacheCamera();

            // 如果 Unity 当前选中的对象就是同一个 GameObject 上的 Camera 组件，也视为选中了这台 Burt 相机。
            if (cachedCamera != null && UnityEditor.Selection.activeObject == cachedCamera)
            {
                // 返回 true，让组件选择和 Camera 选择都能触发 SceneView 跟随。
                return true;
            }

            // 其他选择对象都不属于当前 BurtCameraData，避免场景里多个相机互相覆盖编辑器视图。
            return false;
        }

        // 如果当前相机被选中，就登记成编辑器辅助相机的清屏参考来源。
        private void RegisterEditorClearSourceIfSelected()
        {
            // 只有当前组件确实被用户选中时才登记，避免 ExecuteAlways Update 让未选中的相机覆盖缓存。
            if (!IsSelectedInEditor())
            {
                // 当前相机不是选中对象，直接跳过登记。
                return;
            }

            // 把当前 BurtCameraData 交给清屏工具缓存，供 SceneView/Preview 在 Selection 不稳定时兜底使用。
            BurtCameraClearUtility.RegisterEditorClearCameraData(this);
        }

        // 直接把当前相机登记成编辑器辅助相机的清屏参考来源。
        private void RegisterEditorClearSource()
        {
            // 把当前 BurtCameraData 交给清屏工具缓存，供 SceneView/Preview 在 Selection 不稳定时兜底使用。
            BurtCameraClearUtility.RegisterEditorClearCameraData(this);
        }

        // 如果当前相机被选中，就把清屏设置同步给所有 SceneView 相机。
        private void SyncSelectedEditorSceneViewsClearSettings(CameraClearFlags desiredClearFlags, Color desiredClearColor)
        {
            // 只有当前组件确实被用户选中时才同步 SceneView，避免未选中相机影响编辑器视图。
            if (!IsSelectedInEditor())
            {
                // 当前相机不是选中对象，直接跳过 SceneView 同步。
                return;
            }

            // 遍历 Unity 当前打开的所有 SceneView 窗口，让多 SceneView 布局也能同步。
            foreach (UnityEditor.SceneView sceneView in UnityEditor.SceneView.sceneViews)
            {
                // 如果某个 SceneView 对象为空，就跳过它，避免编辑器窗口关闭瞬间出现空引用。
                if (sceneView == null)
                {
                    // 继续处理下一个 SceneView。
                    continue;
                }

                // 读取这个 SceneView 内部使用的 Unity Camera。
                var sceneCamera = sceneView.camera;

                // 如果 SceneView 还没有创建内部 Camera，就跳过它。
                if (sceneCamera == null)
                {
                    // 继续处理下一个 SceneView。
                    continue;
                }

                // 把选中 Burt 相机的清屏模式同步到 SceneView 的 Unity clearFlags。
                sceneCamera.clearFlags = desiredClearFlags;

                // 把选中 Burt 相机的清屏颜色同步到 SceneView 的 Unity 背景色。
                sceneCamera.backgroundColor = desiredClearColor;

                // 请求这个 SceneView 重绘，让它立即显示新的清屏结果。
                sceneView.Repaint();
            }
        }

        // 请求编辑器视图重绘；这个函数只在编辑器编译，避免 Player 构建引用 UnityEditor。
        private static void RequestEditorViewRepaint()
        {
            // 通知所有 SceneView 下一次编辑器循环重新渲染，确保它能读取最新的 BurtCameraData 清屏配置。
            UnityEditor.SceneView.RepaintAll();
        }
#endif
    }
}
