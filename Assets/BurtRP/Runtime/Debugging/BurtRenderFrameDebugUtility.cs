// 引入全局化命名空间，用来稳定格式化 Camera.depth 等数字。
using System.Globalization;
// 引入 UnityEngine 命名空间，用来访问 Debug、Time 和 Camera。
using UnityEngine;
// 定义 BurtRP 运行时命名空间，让调试工具可以直接访问 Frame 和 request 类型。
namespace Burt.RenderPipeline
{
    // 集中输出 Frame/Stack 分组日志，避免 BurtRenderPipeline 主流程混入大量字符串拼接细节。
    internal static class BurtRenderFrameDebugUtility
    {
        // 输出当前帧的相机栈分组快照；只在资产开关打开时由主管线调用。
        public static void LogFrame(BurtRenderFrame frame)
        {
            // 如果调用方传入空 Frame，就输出短日志帮助定位调用时机问题。
            if (frame == null)
            {
                // 使用统一前缀输出空 Frame 提示。
                Debug.Log(BurtDebugLogUtility.FormatMessage(BurtDebugLogUtility.FramePrefix, "frame is null."));
                // 结束函数，因为没有可遍历的栈组。
                return;
            }
            // 读取当前帧的栈组列表，后续日志只从这份快照取数据。
            var groups = frame.StackGroups;
            // 预估日志容量：基础头部 384 字符，每个栈和 request 额外预留一段空间。
            var builder = BurtDebugStringBuilderPool.Get(384 + groups.Count * 256 + frame.ValidRequestCount * 320);
            // 使用 try/finally 确保即使日志拼接中断也会归还 StringBuilder。
            try
            {
                // 写入统一的 BurtRP + BurtFrame 标题行，方便 Console 按子系统过滤。
                BurtDebugLogUtility.AppendScopedHeaderLine(builder, BurtDebugLogUtility.FramePrefix);
                // 写入 Unity 当前帧号，方便把 Frame 日志和 RenderGraph/Shadow 日志对齐。
                BurtDebugLogUtility.AppendKeyValueLine(builder, "Unity Frame", Time.frameCount);
                // 写入输入 request 总数，帮助确认创建 request 阶段是否漏掉相机。
                BurtDebugLogUtility.AppendKeyValueLine(builder, "Source Request Count", frame.SourceRequestCount);
                // 写入有效 request 总数，帮助和后续真实渲染循环数量对齐。
                BurtDebugLogUtility.AppendKeyValueLine(builder, "Valid Request Count", frame.ValidRequestCount);
                // 写入当前帧识别出的相机栈数量。
                BurtDebugLogUtility.AppendKeyValueLine(builder, "Stack Count", groups.Count);
                // 写入栈列表标题，让后续多行内容结构更清晰。
                builder.AppendLine("Stacks:");
                // 遍历所有栈组；组顺序就是未来 Frame 层可能采用的栈级执行顺序。
                for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
                {
                    // 读取当前栈组，后续输出它的元数据和组内 request。
                    var group = groups[groupIndex];
                    // 如果组对象意外为空，就保留序号输出异常占位。
                    if (group == null)
                    {
                        // 写入空栈组提示，方便定位 Frame 构建问题。
                        builder.Append("  Stack #").Append(groupIndex).AppendLine(": <null group>");
                        // 继续输出下一个组。
                        continue;
                    }
                    // 写入当前栈组的摘要行，包含 StackId、SortLayer、根角色和目标。
                    AppendGroupSummary(builder, groupIndex, group);
                    // 如果这个组内没有 Base 相机且不是编辑器辅助相机，就追加诊断提示。
                    AppendGroupWarnings(builder, group);
                    // 读取组内 request 列表，后续逐行输出每个相机任务。
                    var requests = group.Requests;
                    // 遍历当前组内所有 request。
                    for (var requestIndex = 0; requestIndex < requests.Count; requestIndex++)
                    {
                        // 用和真实执行路径相同的规则生成 RT 生命周期选项，保证日志和实际 Pass 组装一致。
                        var renderOptions = group.CreateRenderOptions(requestIndex);
                        // 把当前 request 的关键信息和 RT 生命周期决策写成一行。
                        AppendRequestLine(builder, requestIndex, requests[requestIndex], renderOptions);
                    }
                }
                // 一次性输出完整 Frame dump，避免每个栈/相机各刷一条 Console。
                Debug.Log(builder.ToString());
            }
            finally
            {
                // 把临时 StringBuilder 归还池，降低持续打开调试日志时的 GC 压力。
                BurtDebugStringBuilderPool.Release(builder);
            }
        }
        // 写入单个相机栈组的摘要信息。
        private static void AppendGroupSummary(System.Text.StringBuilder builder, int groupIndex, BurtCameraStackGroup group)
        {
            // 写入组序号，序号代表当前帧分组后的栈顺序。
            builder.Append("  Stack #").Append(groupIndex);
            // 写入逻辑 StackId，帮助确认 Base/Overlay/UI 是否被放入同一组。
            builder.Append(": StackId=").Append(group.StackId);
            // 写入排序层，帮助确认这个组对应当前帧哪个执行层级。
            builder.Append(" SortLayer=").Append(group.SortLayer);
            // 写入组内 request 数量，帮助快速确认 Overlay/UI 是否被纳入。
            builder.Append(" RequestCount=").Append(group.RequestCount);
            // 写入 Base 数量，帮助发现一个栈缺少 Base 或重复 Base 的配置问题。
            builder.Append(" BaseCount=").Append(group.BaseCameraCount);
            // 写入是否含有 Overlay/UI，用来判断后续是否需要栈内叠加。
            builder.Append(" HasOverlayOrUI=").Append(group.HasOverlayOrUICamera);
            // 写入 Stack 级 RT 计划名称，便于下一步改造时对照。
            builder.Append(" RTPlan=").Append(group.RenderTargetPlanName);
            // 写入未来是否可以使用共享 CameraColor/CameraDepth。
            builder.Append(" SharedRT=").Append(group.ShouldUseSharedRenderTargets);
            // 写入未来是否应该一个 Stack 只做一次 FinalBlit。
            builder.Append(" SingleFinalBlit=").Append(group.ShouldUseSingleFinalBlit);
            // 写入根角色，方便判断这个组是从 Base、Overlay、UI 还是编辑器相机开始。
            builder.Append(" RootRole=").Append(group.RootRole);
            // 写入是否编辑器相机组，方便区分 Game 栈和 SceneView/Preview。
            builder.Append(" EditorOnly=").Append(group.IsEditorCameraGroup);
            // 写入首个目标，后续设计栈级 RT 生命周期时会用这个字段对齐资源归属。
            builder.Append(" Target=").Append(group.TargetIdentifier);
            // 当前组摘要行结束。
            builder.AppendLine();
        }
        // 写入相机栈配置相关的轻量警告，当前只做诊断，不阻止渲染。
        private static void AppendGroupWarnings(System.Text.StringBuilder builder, BurtCameraStackGroup group)
        {
            // 编辑器辅助相机不要求必须有 Base，因此直接跳过警告。
            if (group.IsEditorCameraGroup)
            {
                // 结束函数，避免 SceneView/Preview 输出无意义警告。
                return;
            }
            // 如果非编辑器栈没有 Base，相机栈后续共享颜色/深度时会缺少明确起点。
            if (group.BaseCameraCount == 0)
            {
                // 写入缺少 Base 的提示，帮助你检查 Overlay/UI 是否缺少对应 Base 相机。
                builder.AppendLine("    Warning: 这个相机栈没有 Base，相机栈共享 RT 时需要明确的起点。");
            }
            // 如果同一个组里出现多个 Base，说明 StackId 或 RenderOrder 可能配置得不够唯一。
            if (group.BaseCameraCount > 1)
            {
                // 写入重复 Base 的提示，后续做真正 Camera Stack 时需要避免这种歧义。
                builder.AppendLine("    Warning: 这个相机栈包含多个 Base，请检查 StackId/RenderOrder 是否配置重复。");
            }
        }
        // 写入组内单个 request 的调试行。
        private static void AppendRequestLine(System.Text.StringBuilder builder, int requestIndex, BurtRenderRequest request, BurtRequestRenderOptions renderOptions)
        {
            // 如果 request 为空，就输出占位并返回。
            if (request == null)
            {
                // 写入空 request 行，保留索引方便定位列表问题。
                builder.Append("    #").Append(requestIndex).AppendLine(": <null request>");
                // 结束函数，因为没有更多字段可读。
                return;
            }
            // 读取 Unity 原生 Camera，可能为空，所以后面字段都通过辅助函数做保护。
            var camera = request.Camera;
            // 写入 request 在当前栈内的序号。
            builder.Append("    #").Append(requestIndex);
            // 写入相机名称，方便你直接在 Hierarchy 中定位对象。
            builder.Append(" Camera=").Append(GetCameraName(camera));
            // 写入 Unity CameraType，用来区分 Game、SceneView、Preview 等来源。
            builder.Append(" CameraType=").Append(GetCameraType(camera));
            // 写入 BurtRP request 类型，确认当前相机被解析成哪类渲染任务。
            builder.Append(" RequestType=").Append(request.Type);
            // 写入相机栈角色，确认 Base/Overlay/UI/SceneView/Preview 分类是否符合预期。
            builder.Append(" Role=").Append(request.CameraRole);
            // 写入逻辑栈编号，方便和所属 Stack 摘要对照。
            builder.Append(" StackId=").Append(request.StackId);
            // 写入排序层，方便确认当前帧排序读取的是最新 RenderOrder/Camera.depth。
            builder.Append(" SortLayer=").Append(request.SortLayer);
            // 写入 Unity Camera.depth，方便和 SortLayer 进行对照。
            builder.Append(" Camera.depth=").Append(GetCameraDepth(camera));
            // 写入 Overlay 清颜色意图，后续做栈级 RT 合成时会用它决定是否继承 Base 颜色。
            builder.Append(" OverlayClearColor=").Append(request.OverlayClearsColor);
            // 写入 Overlay 清深度意图，后续做栈级深度共享时会用它决定是否继承 Base 深度。
            builder.Append(" OverlayClearDepth=").Append(request.OverlayClearsDepth);
            // 写入 request 的最终输出目标，帮助检查同栈相机是否指向一致目标。
            builder.Append(" Target=").Append(request.TargetIdentifier);
            // 写入这次 request 的 RT 生命周期决策，方便直接确认 Allocate、FinalBlit 和 Release 是否只发生在栈首/栈尾。
            AppendRenderOptionsInline(builder, renderOptions);
            // 当前 request 行结束。
            builder.AppendLine();
        }
        // 把 RT 生命周期选项追加到 request 行尾，保持 Frame Debug 仍然是一行一个 request。
        private static void AppendRenderOptionsInline(System.Text.StringBuilder builder, BurtRequestRenderOptions renderOptions)
        {
            // 如果没有执行选项，就明确写出占位，方便定位调用链是否漏传。
            if (renderOptions == null)
            {
                // 写入空选项占位。
                builder.Append(" RTOptions=<none>");
                // 结束函数，因为没有更多字段可读。
                return;
            }
            // 写入 RT 计划名称，和 Stack 摘要行的 RTPlan 对齐。
            builder.Append(" RTPlan=").Append(renderOptions.RenderTargetPlanName);
            // 写入 request 在相机栈中的位置，格式为 当前索引/总数量。
            builder.Append(" RTIndex=").Append(renderOptions.RequestIndexInStack).Append('/').Append(renderOptions.RequestCountInStack);
            // 写入是否共享栈级 RT。
            builder.Append(" RTShared=").Append(renderOptions.UseSharedRenderTargets);
            // 写入是否栈首，通常栈首负责 Allocate。
            builder.Append(" RTFirst=").Append(renderOptions.IsFirstRequestInStack);
            // 写入是否栈尾，通常栈尾负责 FinalBlit 和 Release。
            builder.Append(" RTLast=").Append(renderOptions.IsLastRequestInStack);
            // 写入是否申请 CameraColor。
            builder.Append(" AllocateColor=").Append(renderOptions.ShouldAllocateCameraColor);
            // 写入是否申请 CameraDepth。
            builder.Append(" AllocateDepth=").Append(renderOptions.ShouldAllocateCameraDepth);
            // 写入是否执行 FinalBlit。
            builder.Append(" FinalBlit=").Append(renderOptions.ShouldFinalBlit);
            // 写入是否释放 CameraColor。
            builder.Append(" ReleaseColor=").Append(renderOptions.ShouldReleaseCameraColor);
            // 写入是否释放 CameraDepth。
            builder.Append(" ReleaseDepth=").Append(renderOptions.ShouldReleaseCameraDepth);
        }

        // 安全读取相机名称。
        private static string GetCameraName(Camera camera)
        {
            // 相机存在时返回真实名称，否则返回空相机占位文本。
            return camera != null ? camera.name : "<null camera>";
        }
        // 安全读取相机类型。
        private static string GetCameraType(Camera camera)
        {
            // 相机存在时返回 CameraType 字符串，否则返回空相机占位文本。
            return camera != null ? camera.cameraType.ToString() : "<null>";
        }
        // 安全读取相机 depth，并使用固定小数点格式避免不同系统区域设置影响日志。
        private static string GetCameraDepth(Camera camera)
        {
            // 相机存在时格式化 depth，否则返回空相机占位文本。
            return camera != null ? camera.depth.ToString("0.###", CultureInfo.InvariantCulture) : "<null>";
        }
    }
}
