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
        Preview = 4
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

    // 限制同一个 GameObject 上只能挂一个 BurtCameraData。
    [DisallowMultipleComponent]

    // 要求挂 BurtCameraData 的 GameObject 必须同时有 Camera 组件。
    [RequireComponent(typeof(Camera))]

    // 让这个组件在编辑器非播放状态也能执行 Unity 生命周期，方便编辑器里实时同步 Camera.depth。
    [ExecuteAlways]

    // 定义 BurtRP 的相机扩展数据组件。
    public sealed class BurtCameraData : MonoBehaviour
    {
        // 控制这个相机是否参与 BurtRP 渲染。
        [SerializeField] private bool enableRender = true;

        // 控制这个相机在多相机情况下的渲染顺序，数值越小越先渲染。
        [SerializeField] private int renderOrder = 0;

        // 声明这个相机在 BurtRP 相机栈里的角色；第一版只影响 request 分类和同层排序，不改变 Forward 渲染内容。
        [SerializeField] private BurtCameraRole cameraRole = BurtCameraRole.Base;

        // 声明这个相机属于哪个逻辑栈；Base/Overlay/UI 可以用同一个 stackId 表示后续应合成到同一组。
        [SerializeField] private int stackId = 0;

        // 声明 Overlay 相机是否希望清理颜色；当前只记录意图，第一版不会执行复杂叠加合成。
        [SerializeField] private bool overlayClearsColor = false;

        // 声明 Overlay 相机是否希望清理深度；当前只记录意图，第一版不会改变现有 Forward 清屏结果。
        [SerializeField] private bool overlayClearsDepth = true;

        // 控制是否把 BurtRP 的 renderOrder 自动同步到 Unity 原生 Camera.depth；这只是为了让 Unity 原生相机面板和外部工具看到一致深度，不作为 BurtRP 排序的唯一来源。
        [SerializeField] private bool syncRenderOrderToCameraDepth = true;

        // 控制这个相机渲染前如何清理颜色和深度缓冲。
        [SerializeField] private BurtCameraClearMode clearMode = BurtCameraClearMode.Skybox;

        // 控制 SolidColor 模式下使用的清屏颜色。
        [SerializeField] private Color clearColor = new(0.02f, 0.02f, 0.025f, 1f);

        // 缓存当前 GameObject 上的 Camera 组件，避免每帧反复 GetComponent。
        private Camera cachedCamera;

        // 记录上一次已经同步到 Camera.depth 的 renderOrder，用来判断是否需要重新同步。
        private int lastSyncedRenderOrder = int.MinValue;

        // 暴露只读属性，让渲染器可以读取 enableRender，但外部不能随意改字段。
        public bool EnableRender => enableRender;

        // 暴露只读属性，让渲染器可以在每帧创建 request 时直接读取 renderOrder，用于 BurtRP 自己的相机排序。
        public int RenderOrder => renderOrder;

        // 暴露只读属性，让 request 分类逻辑可以读取相机栈角色。
        public BurtCameraRole CameraRole => cameraRole;

        // 暴露只读属性，让排序和调试逻辑可以读取逻辑栈编号。
        public int StackId => stackId;

        // 暴露只读属性，记录 Overlay 相机是否希望清颜色，供后续合成阶段使用。
        public bool OverlayClearsColor => overlayClearsColor;

        // 暴露只读属性，记录 Overlay 相机是否希望清深度，供后续合成阶段使用。
        public bool OverlayClearsDepth => overlayClearsDepth;

        // 暴露只读属性，让渲染器可以读取 clearMode，用于决定清屏策略。
        public BurtCameraClearMode ClearMode => clearMode;

        // 暴露只读属性，让渲染器可以读取 clearColor，用于纯色清屏。
        public Color ClearColor => clearColor;

        // Unity 在组件启用时调用这个函数，适合初始化缓存和同步相机深度。
        private void OnEnable()
        {
            // 获取并缓存当前 GameObject 上的 Camera 组件。
            CacheCamera();

            // 立刻把当前 renderOrder 同步到 Camera.depth。
            SyncRenderOrderToCameraDepth(forceSync: true);
        }

        // Unity 在 Inspector 修改字段时调用这个函数，适合让编辑器里的改动立即生效。
        private void OnValidate()
        {
            // 获取并缓存当前 GameObject 上的 Camera 组件。
            CacheCamera();

            // Inspector 改值后强制同步一次，避免要等启用/禁用相机才刷新。
            SyncRenderOrderToCameraDepth(forceSync: true);
        }

        // Unity 每帧调用这个函数；因为有 ExecuteAlways，编辑器非播放状态也可能调用。
        private void Update()
        {
            // 每帧检查 renderOrder 是否变化，变化时同步到 Camera.depth；BurtRP 排序仍然会在创建 request 时直接读取 RenderOrder。
            SyncRenderOrderToCameraDepth(forceSync: false);
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
    }
}
