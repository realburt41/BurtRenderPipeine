// 引入泛型集合命名空间，用来使用 List<Camera>。
using System.Collections.Generic;

// 引入 UnityEngine 命名空间，用来使用 Camera。
using UnityEngine;

// 引入 UnityEngine.Rendering 命名空间，用来使用 RenderPipeline 和 ScriptableRenderContext。
using UnityEngine.Rendering;

// 定义 Burt 自己的渲染管线命名空间。
namespace Burt.RenderPipeline
{
    // BurtRP 主渲染管线类，负责接收 Unity 渲染入口并分发相机。
    public sealed class BurtRenderPipeline : UnityEngine.Rendering.RenderPipeline
    {
        // 保存管线配置资产。
        private readonly BurtRenderPipelineAsset asset;

        // 创建单相机渲染器。
        private readonly BurtCameraRenderer cameraRenderer = new();

        private readonly BurtRenderGraphAssembler defaultGraphAssembler = new BurtForwardGraphAssembler(); // 创建默认 Forward 组装器，当前阶段所有普通 request 都先使用它。RenderCameras 
        private readonly List<BurtRenderRequest> requests = new();
        
        // BurtRenderPipeline 构造函数。
        public BurtRenderPipeline(BurtRenderPipelineAsset asset)
        {
            // 保存传入的配置资产。
            this.asset = asset;

            // 开启 SRP Batcher。
            GraphicsSettings.useScriptableRenderPipelineBatching = true;
        }

#pragma warning disable 0618

        // Unity 旧版渲染入口。
        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            // 执行共享的渲染逻辑。
            RenderCameras(context, cameras);
        }

#pragma warning restore 0618

        // Unity 新版渲染入口。
        protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
        {
            // 执行共享的渲染逻辑。
            RenderCameras(context, cameras);
        }
        
        private void RenderCameras(ScriptableRenderContext context, Camera[] cameras)
        {
            // 清空上一帧的 request 列表。
            requests.Clear();

            // 遍历 Unity 传入的相机数组。
            foreach (var camera in cameras)
            {
                // 从当前相机创建 BurtRenderRequest。
                var request = BurtRenderRequest.CreateCameraRequest(context, camera);

                // 如果 request 无效，就不加入列表。
                if (!request.IsValid)
                {
                    // 跳过当前相机。
                    continue;
                }

                request.SetGraphAssembler(defaultGraphAssembler); // 给当前 request 指定默认的 Forward 渲染图组装器。
                
                // 把有效 request 加入列表。
                requests.Add(request);
            }

            // 执行所有 request。
            ExecuteRequests(context);
        }

        // 渲染 List<Camera> 的共享逻辑。
        private void RenderCameras(ScriptableRenderContext context, List<Camera> cameras)
        {
            // 清空上一帧的 request 列表。
            requests.Clear();

            // 遍历 Unity 传入的相机列表。
            foreach (var camera in cameras)
            {
                // 从当前相机创建 BurtRenderRequest。
                var request = BurtRenderRequest.CreateCameraRequest(context, camera);

                // 如果 request 无效，就不加入列表。
                if (!request.IsValid)
                {
                    // 跳过当前相机。
                    continue;
                }

                request.SetGraphAssembler(defaultGraphAssembler); // 给当前 request 指定默认的 Forward 渲染图组装器。
                
                // 把有效 request 加入列表。
                requests.Add(request);
            }

            // 执行所有 request。
            ExecuteRequests(context);
        }
        
        // 排序并执行所有 BurtRenderRequest。
        private void ExecuteRequests(ScriptableRenderContext context)
        {
            // 按 request 的 SortLayer 从小到大排序。
            requests.Sort(CompareRequests);

            foreach (var request in requests) // 遍历排序后的每一个渲染请求。
            {
                // 开始单个 request 的处理逻辑。
                if (request == null) // 如果 request 对象为空，说明列表里有异常数据。
                {
                    continue; 
                } 

                if (!request.IsValid) // 如果 request 被标记为无效，说明它不应该参与渲染。
                {
                    continue; 
                } 

                var camera = request.Camera; // 从 request 里取出这次渲染任务对应的 Unity 原生 Camera。

                if (camera == null) // 如果 request 没有关联相机，当前阶段 BurtRP 暂时无法执行它。
                {
                    continue; // 跳过这个没有相机的 request。
                } 

                BeginCameraRendering(context, camera); // 通知 Unity 和外部监听者：这个相机开始渲染。

                try // 使用 try/finally，保证即使渲染过程中报错，也能发出 EndCameraRendering。
                {
                    // 开始真正执行渲染的保护区域。
                    cameraRenderer.Render(context, request, asset); // 把当前 request 交给 BurtCameraRenderer 执行。
                } // 结束真正执行渲染的保护区域。
                finally // 无论渲染成功还是异常，都会执行 finally。
                {
                    // 开始相机结束事件区域。
                    EndCameraRendering(context, camera); // 通知 Unity 和外部监听者：这个相机结束渲染。
                } // 结束相机结束事件区域。
            }
        }

        // request 排序比较函数。
        private static int CompareRequests(BurtRenderRequest left, BurtRenderRequest right)
        {
            // 如果左侧 request 为空，则排到后面。
            if (left == null)
            {
                // 返回 1 表示 left 在 right 后面。
                return 1;
            }

            // 如果右侧 request 为空，则排到后面。
            if (right == null)
            {
                // 返回 -1 表示 left 在 right 前面。
                return -1;
            }

            // 先比较 SortLayer。
            var sortCompare = left.SortLayer.CompareTo(right.SortLayer);

            // 如果 SortLayer 不同，就直接返回比较结果。
            if (sortCompare != 0)
            {
                // 返回 SortLayer 比较结果。
                return sortCompare;
            }

            // 如果其中一个 request 没有相机，就用 0 作为实例 ID。
            var leftId = left.Camera != null ? left.Camera.GetInstanceID() : 0;

            // 如果其中一个 request 没有相机，就用 0 作为实例 ID。
            var rightId = right.Camera != null ? right.Camera.GetInstanceID() : 0;

            // SortLayer 相同时，用 Camera 实例 ID 做稳定排序。
            return leftId.CompareTo(rightId);
        }
        // 读取相机上的 BurtCameraData.RenderOrder。
        private static int GetCameraRenderOrder(Camera camera)
        {
            // 如果相机为空，就返回默认顺序 0。
            if (camera == null)
            {
                // 返回默认渲染顺序。
                return 0;
            }

            // 尝试从相机 GameObject 上获取 BurtCameraData。
            if (camera.TryGetComponent(out BurtCameraData cameraData))
            {
                // 如果获取成功，就返回 BurtCameraData 里的渲染顺序。
                return cameraData.RenderOrder;
            }

            // 如果没有 BurtCameraData，就返回默认顺序 0。
            return 0;
        }
    }
}