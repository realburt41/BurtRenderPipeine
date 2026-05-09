using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 定义 Burt 自己的渲染管线命名空间，和其他 BurtRP 运行时代码保持一致。
namespace Burt.RenderPipeline
{
    // 定义 BurtRP 的渲染请求类型。
    public enum BurtRenderRequestType
    {
        // 主场景相机请求，当前阶段所有普通 Camera 都先归到这个类型。
        MainCamera = 0,

        // UI 相机请求，后面做 UI 合成时会使用。
        UICamera = 1,

        // 预览相机请求，后面做材质预览或编辑器预览时会使用。
        Preview = 2,

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

        // 保存当前请求的剔除结果。
        public CullingResults CullingResults { get; private set; }


        public BurtLightingData LightingData { get; private set; } // Stores lighting data collected for this render request, so passes do not choose lights themselves.
        // 保存当前请求最终要输出到哪个渲染目标。
        public RenderTargetIdentifier TargetIdentifier { get; private set; }
        
        // 保存当前请求的排序层，后续多个 request 会按它排序。
        public int SortLayer { get; private set; }

        // 保存当前请求是否有效，避免后续执行无效 request。
        public bool IsValid { get; private set; }

        public BurtRenderGraphAssembler GraphAssembler { get; private set; } // 保存当前 request 应该使用哪一个渲染图组装器。
        
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


            request.LightingData = BurtLightingData.Default(); // Gives invalid requests safe fallback lighting data in case debug code inspects them.
            // 返回这个无效请求。
            return request;
        }

        public static BurtRenderRequest CreateCameraRequest(
            ScriptableRenderContext context,
            Camera camera)
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
            
            // 尝试从相机获取剔除参数。
            if (!camera.TryGetCullingParameters(out var cullingParameters))
            {
                // 如果获取失败，就返回无效请求。
                return Invalid();
            }
            
            // 使用 Unity 内置剔除系统得到当前相机可见物体。
            var cullingResults = context.Cull(ref cullingParameters);

            // 创建一个新的请求对象。
            var request = new BurtRenderRequest();
            
            // 记录请求类型。
            request.Type = ResolveRequestType(camera, cameraData);
            
            // 记录原生相机。
            request.Camera = camera;

            // 记录 BurtRP 相机数据。
            request.CameraData = cameraData;

            // 记录剔除结果。
            request.CullingResults = cullingResults;


            request.LightingData = BurtLightingData.Create(cullingResults); // Builds request-level lighting data from the same culling results used for drawing.
            // 记录输出目标。
            request.TargetIdentifier = ResolveTargetIdentifier(camera);
            
            // 记录排序层。
            request.SortLayer = ResolveSortLayer(camera, cameraData);

            // 标记请求有效。
            request.IsValid = true;

            // 返回创建好的请求。
            return request;
        }
        
        // 根据相机和相机数据推导请求类型。
        private static BurtRenderRequestType ResolveRequestType(Camera camera, BurtCameraData cameraData)
        {
            // 如果是预览相机，就返回 Preview 类型。
            if (camera.cameraType == CameraType.Preview)
            {
                // 返回预览请求类型。
                return BurtRenderRequestType.Preview;
            }

            // 当前阶段还没有 UI 相机规则，所以默认都作为主相机请求。
            return BurtRenderRequestType.MainCamera;
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
        
        // 根据相机和相机数据推导排序层。
        private static int ResolveSortLayer(Camera camera, BurtCameraData cameraData)
        {
            // 如果存在 BurtCameraData，就使用它的 RenderOrder 作为排序层。
            if (cameraData != null)
            {
                // 返回 BurtCameraData 上配置的渲染顺序。
                return cameraData.RenderOrder;
            }

            // 如果没有 BurtCameraData，就用 Unity 原生 Camera.depth 作为排序层。
            return Mathf.RoundToInt(camera.depth);
        }
    }
}
