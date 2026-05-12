using UnityEngine;
using UnityEngine.Rendering;

// 定义 Burt 自己的渲染管线命名空间，和其他 BurtRP 运行时代码保持一致。
namespace Burt.RenderPipeline
{
    // 定义 BurtRP 的渲染请求类型。
    public enum BurtRenderRequestType
    {
        // 基础场景相机请求，当前仍走现有 Forward 渲染路径。
        BaseCamera = 0,

        // 兼容旧命名；后续代码应优先使用 BaseCamera。
        MainCamera = BaseCamera,

        // Overlay 相机请求，第一版只分类和排序，不做复杂合成。
        OverlayCamera = 1,

        // UI 相机请求，后面做 UI 合成时会使用。
        UICamera = 2,

        // SceneView 相机请求，用来和 GameView/Base 相机区分调试。
        SceneView = 3,

        // 预览相机请求，后面做材质预览或编辑器预览时会使用。
        Preview = 4,

        // 反射探针捕获请求，来自 Unity ReflectionProbe 的 cubemap face 渲染。
        Reflection = 5,

        // 未知请求类型，用来兜底。
        Unknown = 255
    }

    // BurtRenderRequest 表示“一次渲染任务”的上下文数据。
    public class BurtRenderRequest
    {
        // 保存当前渲染请求的类型。
        public BurtRenderRequestType Type { get; private set; }

        // 保存当前请求对应的 Unity 原生相机。
        public Camera Camera { get; private set; }

        // 保存当前请求对应的 BurtRP 相机扩展数据。
        public BurtCameraData CameraData { get; private set; }

        // 保存当前请求解析后的相机栈角色，避免后续排序和调试重复推导。
        public BurtCameraRole CameraRole { get; private set; }

        // 保存当前请求所属的逻辑栈编号；没有 BurtCameraData 时默认归到 0 号栈。
        public int StackId { get; private set; }

        // 记录 Overlay 相机是否希望清颜色；Forward 清屏 Pass 会按这个意图决定是否清颜色。
        public bool OverlayClearsColor { get; private set; }

        // 记录 Overlay 相机是否希望清深度；Forward 清屏 Pass 会按这个意图决定是否清深度。
        public bool OverlayClearsDepth { get; private set; }

        // 保存当前请求的剔除结果。
        public CullingResults CullingResults { get; private set; }


        public BurtLightingData LightingData { get; private set; } // Stores lighting data collected for this render request, so passes do not choose lights themselves.

        public BurtTemporalAARequestState TemporalAA { get; private set; } = BurtTemporalAARequestState.Disabled; // Stores TAA jitter/history state for this request.
        // 保存当前请求最终要输出到哪个渲染目标。
        public RenderTargetIdentifier TargetIdentifier { get; private set; }

        // 保存当前请求的排序层，后续多个 request 会按它排序。
        public int SortLayer { get; private set; }

        // 保存当前请求是否有效，避免后续执行无效 request。
        public bool IsValid { get; private set; }

        public BurtRenderGraphAssembler GraphAssembler { get; private set; } // 保存当前 request 应该使用哪一个渲染图组装器。

        public void SetTemporalAA(BurtTemporalAARequestState temporalAA) // 保存当前 request 的 TAA 状态；v1 是 camera/object velocity + depth/color/confidence history。
        {
            TemporalAA = temporalAA ?? BurtTemporalAARequestState.Disabled;
        }

        public void SetGraphAssembler(BurtRenderGraphAssembler graphAssembler) // 给当前 request 设置渲染图组装器。
        {
            GraphAssembler = graphAssembler; // 保存传入的组装器引用，后面 BurtCameraRenderer 会通过它拿到 Pass 列表。
        }

        // 创建一个无效请求，作为失败时的返回值。
        public static BurtRenderRequest Invalid()
        {
            // 创建一个新的请求对象。
            var request = new BurtRenderRequest();

            // 标记这个请求无效。
            request.IsValid = false;

            // 标记请求类型未知。
            request.Type = BurtRenderRequestType.Unknown;

            // 无效请求没有实际相机，使用 Base 和 0 号栈作为安全兜底值。
            request.CameraRole = BurtCameraRole.Base;
            request.StackId = 0;


            request.LightingData = BurtLightingData.Default(); // Gives invalid requests safe fallback lighting data in case debug code inspects them.
            // 返回这个无效请求。
            return request;
        }

        public static BurtRenderRequest CreateCameraRequest(
            ScriptableRenderContext context,
            Camera camera,
            BurtRenderPipelineAsset asset)
        {
            // 如果相机为空，直接返回无效请求。
            if (camera == null)
            {
                // 返回无效请求。
                return Invalid();
            }

            // 尝试从相机上读取 BurtCameraData。
            camera.TryGetComponent(out BurtCameraData cameraData);

            // 如果相机挂了 BurtCameraData 并且禁用渲染，就返回无效请求。
            if (cameraData != null && !cameraData.EnableRender)
            {
                // 返回无效请求。
                return Invalid();
            }

            BurtEditorGizmoUtility.EmitWorldGeometryForSceneView(camera); // SceneView 剔除前注入编辑器世界几何，恢复 SRP Gizmos/辅助绘制数据。
            BurtTemporalAAUtility.RecoverCameraProjectionForCulling(camera); // 剔除前把 TAA/异常留下的 custom projection 交还给 Unity，避免 GameView 只剩天空盒。

            // 尝试从相机获取剔除参数。
            if (!camera.TryGetCullingParameters(out var cullingParameters))
            {
                // 如果获取失败，就返回无效请求。
                return Invalid();
            }

            // 在 Cull 前写入阴影剔除距离，否则 Unity 可能不会为 DrawShadows 收集可投影物体。
            ApplyShadowCullingParameters(ref cullingParameters, camera, asset);

            // 在 Cull 前声明当前管线需要 reflection probe，否则部分 Unity 版本可能不会为 per-object probe 绑定准备数据。
            ApplyIndirectLightingCullingParameters(ref cullingParameters);

            // 使用 Unity 内置剔除系统得到当前相机可见物体。
            var cullingResults = context.Cull(ref cullingParameters);

            // 创建一个新的请求对象。
            var request = new BurtRenderRequest();

            // 先解析相机角色，后续 request 类型、排序和调试都复用这个结果，保证同一帧内分类一致。
            request.CameraRole = BurtCameraUtility.ResolveCameraRole(camera, cameraData);

            // 记录请求类型，并把相机分类规则集中交给 BurtCameraUtility，避免 request 类继续膨胀。
            request.Type = BurtCameraUtility.ResolveRequestType(request.CameraRole);

            // 记录逻辑栈编号；没有 BurtCameraData 的 Unity 内部相机默认归到 0 号栈。
            request.StackId = cameraData != null ? cameraData.StackId : 0;

            // 判断当前 request 是否真的是 Overlay 相机，因为只有 Overlay 需要这两个清屏意图。
            var isOverlayCamera = request.CameraRole == BurtCameraRole.Overlay;

            // 只在 Overlay 相机上记录是否清理颜色，避免 Base/SceneView 日志显示出无意义的 Overlay 字段。
            request.OverlayClearsColor = isOverlayCamera && cameraData != null && cameraData.OverlayClearsColor;

            // 只在 Overlay 相机上记录是否清理深度，后续共享 StackDepth 时就不会误读 Base 相机的值。
            request.OverlayClearsDepth = isOverlayCamera && cameraData != null && cameraData.OverlayClearsDepth;

            // 记录原生相机。
            request.Camera = camera;

            // 记录 BurtRP 相机数据。
            request.CameraData = cameraData;

            // 记录剔除结果。
            request.CullingResults = cullingResults;


            request.LightingData = BurtLightingData.Create(cullingResults); // Builds request-level lighting data from the same culling results used for drawing.
            // 记录输出目标。
            request.TargetIdentifier = ResolveTargetIdentifier(camera);

            // 每帧创建 request 时都重新读取 BurtCameraData.RenderOrder 或 Camera.depth，避免排序结果只在相机启用/禁用时才刷新。
            request.SortLayer = BurtCameraSortUtility.ResolveSortLayer(camera, cameraData);

            // 标记请求有效。
            request.IsValid = true;

            // 返回创建好的请求。
            return request;
        }

        private static void ApplyShadowCullingParameters( // 定义阴影剔除参数写入函数，专门在 Unity Cull 前调整 shadowDistance。
            ref ScriptableCullingParameters cullingParameters, // 接收 Unity 即将用于 Cull 的参数引用，函数会直接修改其中的 shadowDistance。
            Camera camera, // 接收当前相机，用来限制阴影距离不能超过相机远裁剪面。
            BurtRenderPipelineAsset asset) // 接收当前管线资产，用来读取主光阴影距离和总开关。
        {
            var shadowDistance = BurtShadowData.DefaultMainLightShadowDistance; // 先使用 BurtRP 默认阴影距离，保证 asset 缺失时也能收集基础阴影 caster。

            if (asset != null) // 如果当前有管线资产，就优先使用资产上的阴影设置。
            {
                shadowDistance = asset.EnableMainLightShadows ? asset.MainLightShadowDistance : 0f; // 资产关闭主光阴影时把 shadowDistance 清零，避免 Unity 做无意义阴影剔除。
            }

            if (camera != null) // 如果相机有效，就用相机远裁剪面限制阴影距离。
            {
                shadowDistance = Mathf.Min(shadowDistance, camera.farClipPlane); // 阴影距离不能超过相机能看到的最远范围，避免扩大无效阴影剔除。
            }

            cullingParameters.shadowDistance = Mathf.Max(0f, shadowDistance); // 把最终非负距离写入 Unity culling 参数，让 DrawShadows 拿到正确的投影物集合。
        }

        private static void ApplyIndirectLightingCullingParameters(ref ScriptableCullingParameters cullingParameters) // 定义间接光剔除参数写入函数，确保 Unity 准备 Reflection Probe 数据。
        {
            cullingParameters.cullingOptions |= CullingOptions.NeedsReflectionProbes; // 告诉 Unity 当前 SRP 需要反射探针数据，后续 DrawRenderers 的 perObjectData 才能绑定 unity_SpecCube0。
        }


        // 根据相机推导输出目标。
        private static RenderTargetIdentifier ResolveTargetIdentifier(Camera camera)
        {
            // 如果相机设置了 targetTexture，就把请求输出到这个 RenderTexture。
            if (camera.targetTexture != null)
            {
                // 返回相机指定的 RenderTexture 作为输出目标。
                return new RenderTargetIdentifier(camera.targetTexture);
            }

            // 如果没有 targetTexture，就输出到当前 CameraTarget，也就是 GameView/backbuffer。
            return new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget);
        }

    }
}
