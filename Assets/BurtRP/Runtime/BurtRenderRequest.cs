using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

// 定义 Burt 自己的渲染管线命名空间，和其他 BurtRP 运行时代码保持一致。
namespace Burt.RenderPipeline
{
    public enum BurtNonCameraRenderStage
    {
        BeforeCameras = 0,
        AfterCameras = 1,
    }

    public interface IBurtNonCameraRenderRequest
    {
        string Name { get; }
        BurtNonCameraRenderStage Stage { get; }
        int SortOrder { get; }
        bool IsValid { get; }
        void Execute(ScriptableRenderContext context, BurtRenderPipelineAsset asset);
    }

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
        private const int MaxPooledRequestCount = 32;
        private static readonly Stack<BurtRenderRequest> RequestPool = new Stack<BurtRenderRequest>();
        private static readonly ProfilerMarker CullMarker = new ProfilerMarker("BRP.Camera.Cull");
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

        public BurtRenderGraphAssembler GraphAssembler { get; private set; } // ???? request ??????????????
        internal BurtMainLightShadowCascadeCache MainLightShadowCascadeCache { get; } = new BurtMainLightShadowCascadeCache(); // Cache per-request cascade data so every pass consumes one identical shadow layout.
        private BurtRenderPipelineAsset cachedResolvedMainLightShadowAsset; // Remembers which asset the merged shadow settings belong to.
        private BurtShadowData cachedResolvedMainLightShadowData; // Reuses the merged shadow settings across setup/shadow/deferred passes.

        public void SetTemporalAA(BurtTemporalAARequestState temporalAA) // ???? request ? TAA ???v1 ? camera/object velocity + depth/color/confidence history?
        {
            TemporalAA = temporalAA ?? BurtTemporalAARequestState.Disabled;
        }

        public void SetGraphAssembler(BurtRenderGraphAssembler graphAssembler) // ??? request ?????????
        {
            GraphAssembler = graphAssembler; // ????????????? BurtCameraRenderer ?????? Pass ???
        }

        internal bool TryGetCachedResolvedMainLightShadowData(BurtRenderPipelineAsset asset, out BurtShadowData shadowData) // Lets later passes reuse the already merged shadow settings for this request.
        {
            if (cachedResolvedMainLightShadowData != null && cachedResolvedMainLightShadowAsset == asset)
            {
                shadowData = cachedResolvedMainLightShadowData;
                return true;
            }

            shadowData = null;
            return false;
        }

        internal void CacheResolvedMainLightShadowData(BurtRenderPipelineAsset asset, BurtShadowData shadowData) // Updates the request-local merged shadow settings cache.
        {
            cachedResolvedMainLightShadowAsset = asset;
            cachedResolvedMainLightShadowData = shadowData;
        }

        // ???????????????????
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

        public static BurtRenderRequest PrepareCameraRequest(
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

            // 创建一个新的请求对象。
            var request = Acquire();

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

            request.LightingData = BurtLightingData.Default(); // Culling and request-local lighting are populated inside the camera rendering lifecycle.
            // 记录输出目标。
            request.TargetIdentifier = ResolveTargetIdentifier(camera);

            // 每帧创建 request 时都重新读取 BurtCameraData.RenderOrder 或 Camera.depth，避免排序结果只在相机启用/禁用时才刷新。
            request.SortLayer = BurtCameraSortUtility.ResolveSortLayer(camera, cameraData);

            // 标记请求有效。
            request.IsValid = true;

            // 返回创建好的请求。
            return request;
        }

        internal static void Release(BurtRenderRequest request)
        {
            if (request == null || !request.IsValid || RequestPool.Count >= MaxPooledRequestCount)
            {
                return;
            }

            request.Type = BurtRenderRequestType.Unknown;
            request.Camera = null;
            request.CameraData = null;
            request.CameraRole = BurtCameraRole.Base;
            request.StackId = 0;
            request.OverlayClearsColor = false;
            request.OverlayClearsDepth = false;
            request.CullingResults = default;
            request.LightingData = null;
            request.TemporalAA = BurtTemporalAARequestState.Disabled;
            request.TargetIdentifier = default;
            request.SortLayer = 0;
            request.IsValid = false;
            request.GraphAssembler = null;
            request.MainLightShadowCascadeCache.Clear();
            request.cachedResolvedMainLightShadowAsset = null;
            request.cachedResolvedMainLightShadowData = null;
            RequestPool.Push(request);
        }

        private static BurtRenderRequest Acquire()
        {
            return RequestPool.Count > 0 ? RequestPool.Pop() : new BurtRenderRequest();
        }

        public bool TryCull(
            ScriptableRenderContext context,
            BurtRenderPipelineAsset asset)
        {
            using var cullScope = CullMarker.Auto();
            if (!IsValid || Camera == null)
            {
                return false;
            }

            BurtEditorGizmoUtility.EmitWorldGeometryForSceneView(Camera); // SceneView geometry must be emitted after BeginCameraRendering and before culling.
            BurtTemporalAAUtility.RecoverCameraProjectionForCulling(Camera); // Recover any previous jitter before Unity derives culling planes.
            PostProcessUtility.UpdateVolumeStack(Camera, asset); // Refresh volume-driven shadow distance while the camera lifecycle is active.

            if (!Camera.TryGetCullingParameters(out var cullingParameters))
            {
                LightingData = BurtLightingData.Default();
                return false;
            }

            ApplyShadowCullingParameters(ref cullingParameters, Camera, asset);
            ApplyIndirectLightingCullingParameters(ref cullingParameters);

            var perObjectShadowLightMaskScope = BurtPerObjectShadowUtility.BeginDirectionalLightRenderingLayerMaskOverrideForCulling();
            try
            {
                CullingResults = context.Cull(ref cullingParameters);
            }
            finally
            {
                perObjectShadowLightMaskScope?.Dispose();
            }

            var applyAtmosphereTransmittance = Type != BurtRenderRequestType.Preview
                && Type != BurtRenderRequestType.UICamera;
            LightingData = BurtLightingData.Create(CullingResults, applyAtmosphereTransmittance);
            return true;
        }

        private static void ApplyShadowCullingParameters( // 定义阴影剔除参数写入函数，专门在 Unity Cull 前调整 shadowDistance。
            ref ScriptableCullingParameters cullingParameters, // 接收 Unity 即将用于 Cull 的参数引用，函数会直接修改其中的 shadowDistance。
            Camera camera, // 接收当前相机，用来限制阴影距离不能超过相机远裁剪面。
            BurtRenderPipelineAsset asset) // 接收当前管线资产，用来读取主光阴影距离和总开关。
        {
            var shadowDistance = BurtShadowUtility.ResolveMainLightShadowDistance(asset); // 统一解析资产和 Global Volume 覆盖后的阴影距离。

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
    internal sealed class BurtMainLightShadowCascadeCache // Stores the request-local cascade layout so every main-light shadow consumer reads one consistent result.
    {
        private const int MaxCascadeCount = BurtShadowData.MaxMainLightShadowCascadeCount;

        public Matrix4x4[] ViewMatrices { get; } = new Matrix4x4[MaxCascadeCount];
        public Matrix4x4[] ProjectionMatrices { get; } = new Matrix4x4[MaxCascadeCount];
        public Matrix4x4[] WorldToShadowMatrices { get; } = new Matrix4x4[MaxCascadeCount];
        public ShadowSplitData[] SplitDatas { get; } = new ShadowSplitData[MaxCascadeCount];
        public Vector4[] CascadeSpheres { get; } = new Vector4[MaxCascadeCount];
        public Vector4[] CascadeAtlasRects { get; } = new Vector4[MaxCascadeCount];
        public bool IsValid { get; private set; }
        public int CascadeCount { get; private set; }
        public int TileResolution { get; private set; }
        public int AtlasResolution { get; private set; }

        private int cachedMainLightIndex = -1;
        private float cachedNearPlane;
        private int cachedShadowResolution;
        private int cachedCascadeCountSetting;
        private float cachedCascadeSplit1;
        private float cachedCascadeSplit2;
        private float cachedCascadeSplit3;

        public BurtMainLightShadowCascadeCache()
        {
            Clear();
        }

        public bool Matches(BurtShadowData shadowData) // Validates whether the current cache still matches the resolved request shadow settings.
        {
            return IsValid
                && shadowData != null
                && shadowData.HasMainLightShadow
                && cachedMainLightIndex == shadowData.MainLightIndex
                && cachedShadowResolution == shadowData.MainLightShadowResolution
                && cachedCascadeCountSetting == shadowData.MainLightShadowCascadeCount
                && Mathf.Approximately(cachedNearPlane, shadowData.MainLightShadowNearPlane)
                && Mathf.Approximately(cachedCascadeSplit1, shadowData.MainLightShadowCascadeSplit1)
                && Mathf.Approximately(cachedCascadeSplit2, shadowData.MainLightShadowCascadeSplit2)
                && Mathf.Approximately(cachedCascadeSplit3, shadowData.MainLightShadowCascadeSplit3);
        }

        public void Store(BurtShadowData shadowData, int cascadeCount, int tileResolution, int atlasResolution) // Captures the shadow-layout key and computed output sizes for later pass reuse.
        {
            cachedMainLightIndex = shadowData != null ? shadowData.MainLightIndex : -1;
            cachedNearPlane = shadowData != null ? shadowData.MainLightShadowNearPlane : 0f;
            cachedShadowResolution = shadowData != null ? shadowData.MainLightShadowResolution : 0;
            cachedCascadeCountSetting = shadowData != null ? shadowData.MainLightShadowCascadeCount : 0;
            cachedCascadeSplit1 = shadowData != null ? shadowData.MainLightShadowCascadeSplit1 : 0f;
            cachedCascadeSplit2 = shadowData != null ? shadowData.MainLightShadowCascadeSplit2 : 0f;
            cachedCascadeSplit3 = shadowData != null ? shadowData.MainLightShadowCascadeSplit3 : 0f;
            CascadeCount = cascadeCount;
            TileResolution = tileResolution;
            AtlasResolution = atlasResolution;
            IsValid = cascadeCount > 0 && tileResolution > 0 && atlasResolution > 0;
        }

        public void Clear() // Resets the cache to the same disabled shadow defaults expected by the runtime globals.
        {
            IsValid = false;
            CascadeCount = 0;
            TileResolution = 0;
            AtlasResolution = 0;
            cachedMainLightIndex = -1;
            cachedNearPlane = 0f;
            cachedShadowResolution = 0;
            cachedCascadeCountSetting = 0;
            cachedCascadeSplit1 = 0f;
            cachedCascadeSplit2 = 0f;
            cachedCascadeSplit3 = 0f;

            for (var cascadeIndex = 0; cascadeIndex < MaxCascadeCount; cascadeIndex++)
            {
                ViewMatrices[cascadeIndex] = Matrix4x4.identity;
                ProjectionMatrices[cascadeIndex] = Matrix4x4.identity;
                WorldToShadowMatrices[cascadeIndex] = Matrix4x4.identity;
                SplitDatas[cascadeIndex] = default;
                CascadeSpheres[cascadeIndex] = new Vector4(0f, 0f, 0f, -1f);
                CascadeAtlasRects[cascadeIndex] = new Vector4(0f, 0f, 1f, 1f);
            }
        }
    }

}
