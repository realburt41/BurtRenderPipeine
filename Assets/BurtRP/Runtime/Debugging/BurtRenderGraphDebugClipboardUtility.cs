using System.Collections.Generic; // 引入泛型集合，用来按 RequestType 分别缓存最近一次 RenderGraph dump。
using UnityEngine; // 引入 UnityEngine 命名空间，用 Time、GUIUtility 和 Debug 完成帧号记录与剪切板写入。

namespace Burt.RenderPipeline // 定义 BurtRP 运行时命名空间，让渲染器、资产和编辑器包装方法都能访问同一份缓存。
{
    public static class BurtRenderGraphDebugClipboardUtility // 定义 RenderGraph Debug 剪切板工具，负责缓存最近一次 dump 并处理一帧复制请求。
    {
        private struct DumpRecord // 保存单个 request 类型最近一次 RenderGraph Debug。
        {
            public string Dump; // 完整 RenderGraph Debug 文本。
            public string RequestType; // 文本对应的 request 类型摘要。
            public string CameraName; // 文本对应的相机名称摘要。
            public int Frame; // 文本生成时的 Unity 帧号。

            public bool IsValid => !string.IsNullOrEmpty(Dump); // 判断这条记录是否有可复制内容。

            public string Summary // 生成 Inspector 使用的一行摘要。
            {
                get
                {
                    return "Frame=" + Frame + " Request=" + RequestType + " Camera=" + CameraName + " Length=" + (Dump != null ? Dump.Length : 0);
                }
            }
        }

        private static readonly Dictionary<BurtRenderRequestType, DumpRecord> latestDumpsByType = new Dictionary<BurtRenderRequestType, DumpRecord>(); // 按 request 类型分别保存最近一次 dump，避免 SceneView 覆盖 Preview/Reflection 诊断。
        private static string latestDump; // 保存最近一次 RenderGraph Debug 完整文本，Inspector 按钮会把它复制到系统剪切板。
        private static string latestRequestType; // 保存最近一次 dump 对应的 request 类型，方便 Inspector 显示摘要。
        private static string latestCameraName; // 保存最近一次 dump 对应的相机名称，方便确认复制的是哪个窗口或相机。
        private static int latestFrame = -1; // 保存最近一次 dump 的 Unity 帧号，帮助判断剪切板内容是不是刚生成的。
        private static bool copyNextDumpToClipboard; // 标记用户是否点击了“下一帧复制”，下一次生成 dump 时会自动写入剪切板。
        private static bool copyNextDumpHasRequestTypeFilter; // 标记一次性复制是否只等待某一种 request，避免 SceneView 抢先消费 Preview/Reflection 请求。
        private static BurtRenderRequestType copyNextDumpRequestTypeFilter; // 保存一次性复制要等待的 request 类型。

        public static bool HasLatestDump => !string.IsNullOrEmpty(latestDump); // 暴露当前是否已经缓存过有效 dump。

        public static bool ShouldCaptureNextDump => copyNextDumpToClipboard; // 暴露是否有一次性复制请求，渲染器用它决定即使关闭常驻 Debug 也要生成一帧 dump。

        public static string LatestDumpSummary // 暴露最近一次 dump 摘要，Inspector 用它显示当前缓存状态。
        {
            get // 定义只读 getter。
            {
                if (!HasLatestDump) // 如果还没有任何 dump 被缓存。
                {
                    return "当前还没有缓存 RenderGraph Debug。"; // 返回中文提示，告诉用户需要先渲染一帧或点下一帧复制。
                }

                return "Frame=" + latestFrame + " Request=" + latestRequestType + " Camera=" + latestCameraName + " Length=" + latestDump.Length; // 返回帧号、request、相机和文本长度。
            }
        }

        public static bool HasLatestDumpForRequestType(BurtRenderRequestType requestType) // 查询指定 request 类型是否已经缓存过 dump。
        {
            return latestDumpsByType.TryGetValue(requestType, out var record) && record.IsValid; // 只有对应记录存在且有完整文本时才返回 true。
        }

        public static string GetLatestDumpSummary(BurtRenderRequestType requestType) // 返回指定 request 类型最近一次 dump 摘要。
        {
            if (!latestDumpsByType.TryGetValue(requestType, out var record) || !record.IsValid) // 如果指定类型还没有渲染过或未开启捕获。
            {
                return requestType + ": 当前还没有缓存 RenderGraph Debug。"; // 给 Inspector 一个明确的空状态。
            }

            return requestType + ": " + record.Summary; // 返回带 request 类型前缀的摘要，方便横向比较 SceneView/Preview/Reflection。
        }

        public static bool ShouldCaptureNextDumpForRequest(BurtRenderRequest request) // 判断当前 request 是否命中一次性复制请求。
        {
            if (!copyNextDumpToClipboard) // 如果用户没有点“一帧复制”。
            {
                return false; // 不需要为了复制额外生成 dump。
            }

            if (!copyNextDumpHasRequestTypeFilter) // 没有过滤器时保持旧行为：下一次任意 request 都可复制。
            {
                return true; // 当前 request 需要捕获。
            }

            return request != null && request.Type == copyNextDumpRequestTypeFilter; // 有过滤器时只有目标 request 能触发捕获和复制。
        }

        public static void StoreLatestDump( // 保存最新生成的 RenderGraph Debug 文本。
            BurtRenderRequest request, // 接收当前 dump 对应的渲染请求，用来提取 request 类型和相机名。
            string dump) // 接收完整 RenderGraph Debug 文本。
        {
            if (string.IsNullOrEmpty(dump)) // 如果传入文本为空，就不覆盖已有缓存。
            {
                return; // 直接返回，避免按钮复制到空内容。
            }

            latestDump = dump; // 保存完整 dump，后续按钮会直接复制这一份。
            latestRequestType = request != null ? request.Type.ToString() : "<none>"; // 保存 request 类型，request 为空时使用占位。
            latestCameraName = request != null && request.Camera != null ? request.Camera.name : "<none>"; // 保存相机名称，相机为空时使用占位。
            latestFrame = Time.frameCount; // 保存当前 Unity 帧号，帮助判断这份 dump 是否来自最新渲染。

            if (request != null) // 有 request 时按类型额外保存一份索引。
            {
                latestDumpsByType[request.Type] = new DumpRecord
                {
                    Dump = dump,
                    RequestType = latestRequestType,
                    CameraName = latestCameraName,
                    Frame = latestFrame
                }; // 记录同一类型的最近一次 dump，后续 Preview/Reflection 按钮不会被 SceneView 覆盖。
            }
        }

        public static void RequestCopyNextDumpToClipboard() // 请求下一次生成 RenderGraph Debug 时自动复制到剪切板。
        {
            copyNextDumpToClipboard = true; // 打开一次性请求标记，渲染器消费后会立刻清掉。
            copyNextDumpHasRequestTypeFilter = false; // 无过滤器时保持旧按钮语义：下一次任何 request 都可以被复制。
        }

        public static void RequestCopyNextDumpToClipboard(BurtRenderRequestType requestType) // 请求下一次指定类型的 RenderGraph Debug 自动复制到剪切板。
        {
            copyNextDumpToClipboard = true; // 打开一次性请求标记。
            copyNextDumpHasRequestTypeFilter = true; // 开启 request 类型过滤，避免 SceneView 抢先消费。
            copyNextDumpRequestTypeFilter = requestType; // 记录需要等待的 request 类型。
        }

        public static bool ConsumeCopyNextDumpRequest(BurtRenderRequest request) // 如果当前 request 命中过滤条件，就消费并清除一次性复制请求。
        {
            if (!ShouldCaptureNextDumpForRequest(request)) // 如果当前没有按钮请求，或当前 request 不是目标类型。
            {
                return false; // 返回 false，告诉调用方不需要复制。
            }

            copyNextDumpToClipboard = false; // 清掉请求，保证一次点击只复制一帧 dump。
            copyNextDumpHasRequestTypeFilter = false; // 清掉过滤器，下一次按钮重新指定。
            return true; // 返回 true，告诉调用方本次应该执行复制。
        }

        public static bool ConsumeCopyNextDumpRequest() // 保留旧入口，兼容无 request 上下文的调用。
        {
            return ConsumeCopyNextDumpRequest(null); // 没有 request 时只有无过滤器请求会被消费。
        }

        public static bool CopyLatestDumpToClipboard() // 把最近一次 dump 写入系统剪切板。
        {
            if (!HasLatestDump) // 如果还没有缓存内容。
            {
                return false; // 返回 false，按钮可以据此提示用户先生成一帧 dump。
            }

            GUIUtility.systemCopyBuffer = latestDump; // 把完整 RenderGraph Debug 文本写入系统剪切板。
            return true; // 返回 true，表示复制成功。
        }

        public static bool CopyLatestDumpToClipboard(BurtRenderRequestType requestType) // 把指定 request 类型最近一次 dump 写入系统剪切板。
        {
            if (!latestDumpsByType.TryGetValue(requestType, out var record) || !record.IsValid) // 如果该类型还没有缓存。
            {
                return false; // 返回 false，让 Inspector 提示用户先生成对应窗口的 dump。
            }

            GUIUtility.systemCopyBuffer = record.Dump; // 复制指定 request 类型的完整文本。
            return true; // 返回 true，表示复制成功。
        }

        public static void ClearLatestDump() // 清空最近一次缓存的 RenderGraph Debug 文本。
        {
            latestDump = null; // 清空完整文本。
            latestRequestType = null; // 清空 request 类型摘要。
            latestCameraName = null; // 清空相机名摘要。
            latestFrame = -1; // 重置帧号。
            latestDumpsByType.Clear(); // 同步清掉所有按 request 类型缓存的 dump。
            copyNextDumpToClipboard = false; // 同时取消还没被消费的一次性复制请求。
            copyNextDumpHasRequestTypeFilter = false; // 清掉一次性过滤器。
        }

        public static void CopyLatestDumpToClipboardAndLog() // 复制最近一次 dump，并输出一条短提示。
        {
            if (!CopyLatestDumpToClipboard()) // 尝试复制，如果没有缓存内容则失败。
            {
                Debug.LogWarning("[BurtRP][RenderGraphClipboard] 当前没有可复制的 RenderGraph Debug。"); // 输出短警告，不打印长 dump。

                return; // 没有内容时结束。
            }

            Debug.Log("[BurtRP][RenderGraphClipboard] 已复制最近一次 RenderGraph Debug 到剪切板：" + LatestDumpSummary); // 输出短确认，不再把完整 dump 刷到 Console。
        }

        public static void CopyLatestDumpToClipboardAndLog(BurtRenderRequestType requestType) // 复制指定 request 类型的 dump，并输出短提示。
        {
            if (!CopyLatestDumpToClipboard(requestType)) // 尝试复制指定类型的缓存。
            {
                Debug.LogWarning("[BurtRP][RenderGraphClipboard] 当前没有可复制的 " + requestType + " RenderGraph Debug。"); // 输出短警告，不打印长 dump。

                return; // 没有内容时结束。
            }

            Debug.Log("[BurtRP][RenderGraphClipboard] 已复制 " + requestType + " RenderGraph Debug 到剪切板：" + GetLatestDumpSummary(requestType)); // 输出短确认。
        }
    }
}
