// 引入泛型集合命名空间，用来用 IList<BurtRenderRequest> 接收已经排序好的 request 列表。
using System.Collections.Generic;

// 引入全局化命名空间，用来把 Camera.depth 格式化成不受系统小数点设置影响的字符串。
using System.Globalization;

// 引入文本构建命名空间，用来用 StringBuilder 拼接多行日志，减少字符串临时对象。
using System.Text;

// 引入 UnityEngine 命名空间，用来访问 Debug、Time、Camera 等 Unity 类型。
using UnityEngine;

// 定义 Burt 自己的渲染管线命名空间，保证调试工具能直接访问 BurtRenderRequest。
namespace Burt.RenderPipeline
{
    // 集中处理相机排序调试输出，避免 BurtRenderPipeline 主流程里混入大量日志拼接细节。
    internal static class BurtCameraDebugUtility
    {
        // 输出当前帧已经排序完成的 request 列表，日志内容必须和实际执行顺序一致。
        public static void LogSortedRequests(IList<BurtRenderRequest> requests)
        {
            // 如果调用方传入空列表引用，就输出一条短日志帮助定位调用问题。
            if (requests == null)
            {
                // 输出空列表提示，避免后续访问 Count 时抛 NullReferenceException。
                Debug.Log("[BurtRP][CameraSort] requests is null.");

                // 结束函数，因为没有可遍历的 request。
                return;
            }

            // 预分配一个大致容量，避免多相机时 StringBuilder 频繁扩容。
            var builder = new StringBuilder(256 + requests.Count * 160);

            // 写入日志标题，包含 Unity 当前帧号和 request 数量，方便和其他每帧日志对齐。
            builder.Append("[BurtRP][CameraSort] Frame ").Append(Time.frameCount).Append(": ").Append(requests.Count).AppendLine(" request(s)");

            // 遍历已经排序后的 request 列表，index 就是后续实际执行顺序。
            for (var index = 0; index < requests.Count; index++)
            {
                // 读取当前位置的 request，后续要从里面取相机和排序信息。
                var request = requests[index];

                // 如果 request 为空，就在日志里保留这个位置，帮助发现异常列表内容。
                if (request == null)
                {
                    // 写入空 request 行，仍然包含序号以便定位排序列表的位置。
                    builder.Append("  #").Append(index).AppendLine(": <null request>");

                    // 继续处理下一个 request，避免访问空对象字段。
                    continue;
                }

                // 从 request 里取出 Unity 原生 Camera，可能为空，所以后面所有读取都要做空值保护。
                var camera = request.Camera;

                // 从 request 里取出 BurtCameraData，可能为空，用来输出“是否有 BurtCameraData”。
                var cameraData = request.CameraData;

                // 读取相机名称；没有相机时输出占位文本，避免日志中断。
                var cameraName = camera != null ? camera.name : "<null camera>";

                // 读取 Unity 原生 CameraType；没有相机时输出占位文本。
                var cameraType = camera != null ? camera.cameraType.ToString() : "<null>";

                // 读取 Unity 原生 Camera.depth；没有相机时输出占位文本，并使用 InvariantCulture 保证小数点格式稳定。
                var cameraDepth = camera != null ? camera.depth.ToString("0.###", CultureInfo.InvariantCulture) : "<null>";

                // 把 BurtCameraData 是否存在转成短文本，方便在 Console 中快速扫描。
                var hasCameraData = cameraData != null ? "Yes" : "No";

                // 写入当前 request 的序号，序号代表实际渲染执行顺序。
                builder.Append("  #").Append(index);

                // 写入相机名称，方便直接在层级里找到对应 Camera。
                builder.Append(" CameraName=").Append(cameraName);

                // 写入 Unity 原生 CameraType，用来区分 Game、SceneView、Preview 等相机来源。
                builder.Append(" CameraType=").Append(cameraType);

                // 写入 BurtRP request 类型，用来确认 ResolveRequestType 的分类结果。
                builder.Append(" RequestType=").Append(request.Type);

                // 写入 BurtRP 实际用于排序的 SortLayer，用来确认 RenderOrder/Camera.depth 是否已经被当前帧读取。
                builder.Append(" SortLayer=").Append(request.SortLayer);

                // 写入 Unity 原生 Camera.depth，用来和 SortLayer 对比，判断是否由 BurtCameraData 覆盖排序。
                builder.Append(" Camera.depth=").Append(cameraDepth);

                // 写入是否挂了 BurtCameraData，用来区分 SortLayer 来源是 RenderOrder 还是 Camera.depth。
                builder.Append(" HasBurtCameraData=").Append(hasCameraData);

                // 结束当前 request 的日志行，让下一条 request 从新行开始。
                builder.AppendLine();
            }

            // 一次性输出整段排序列表，避免每个 request 单独刷一条 Console 日志。
            Debug.Log(builder.ToString());
        }
    }
}
