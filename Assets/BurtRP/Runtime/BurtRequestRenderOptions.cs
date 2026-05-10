namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让执行选项可以被 Pipeline、Renderer 和 Assembler 共同访问。
{
    public sealed class BurtRequestRenderOptions // 定义单个 BurtRenderRequest 在相机栈中的 RenderTarget 生命周期选项。
    {
        public bool UseSharedRenderTargets { get; } // 表示当前 request 是否复用栈级 CameraColor 和 CameraDepth。

        public bool IsFirstRequestInStack { get; } // 表示当前 request 是否是相机栈里的第一个 request。

        public bool IsLastRequestInStack { get; } // 表示当前 request 是否是相机栈里的最后一个 request。

        public int RequestIndexInStack { get; } // 保存当前 request 在栈内的索引，方便调试和后续插入栈级 Pass。

        public int RequestCountInStack { get; } // 保存当前相机栈里的 request 总数，方便判断首尾和输出日志。

        public int StackId { get; } // 保存当前 request 所属的逻辑相机栈编号，和 BurtCameraData.StackId 对齐。

        public string RenderTargetPlanName { get; } // 保存当前栈采用的 RenderTarget 计划名称，方便 RenderGraph Debug 时追踪策略来源。

        public bool ShouldAllocateCameraColor { get; } // 表示当前 request 是否需要申请 CameraColor 临时 RT。

        public bool ShouldAllocateCameraDepth { get; } // 表示当前 request 是否需要申请 CameraDepth 临时 RT。

        public bool ShouldFinalBlit { get; } // 表示当前 request 是否需要把 CameraColor 拷贝到最终相机目标。

        public bool ShouldReleaseCameraColor { get; } // 表示当前 request 是否需要释放 CameraColor 临时 RT。

        public bool ShouldReleaseCameraDepth { get; } // 表示当前 request 是否需要释放 CameraDepth 临时 RT。

        private BurtRequestRenderOptions( // 定义私有构造函数，强制外部通过命名工厂创建语义明确的选项。
            bool useSharedRenderTargets, // 接收是否共享栈级 RT 的标记。
            bool isFirstRequestInStack, // 接收是否为栈内第一个 request。
            bool isLastRequestInStack, // 接收是否为栈内最后一个 request。
            int requestIndexInStack, // 接收当前 request 的栈内索引。
            int requestCountInStack, // 接收当前相机栈的 request 总数。
            int stackId, // 接收逻辑相机栈编号。
            string renderTargetPlanName, // 接收 RenderTarget 计划名称。
            bool shouldAllocateCameraColor, // 接收是否申请 CameraColor 的决策。
            bool shouldAllocateCameraDepth, // 接收是否申请 CameraDepth 的决策。
            bool shouldFinalBlit, // 接收是否执行 FinalBlit 的决策。
            bool shouldReleaseCameraColor, // 接收是否释放 CameraColor 的决策。
            bool shouldReleaseCameraDepth) // 接收是否释放 CameraDepth 的决策。
        {
            UseSharedRenderTargets = useSharedRenderTargets; // 保存是否共享栈级 RT 的标记。

            IsFirstRequestInStack = isFirstRequestInStack; // 保存首个 request 标记。

            IsLastRequestInStack = isLastRequestInStack; // 保存最后一个 request 标记。

            RequestIndexInStack = requestIndexInStack; // 保存栈内索引。

            RequestCountInStack = requestCountInStack; // 保存栈内 request 总数。

            StackId = stackId; // 保存逻辑栈编号。

            RenderTargetPlanName = NormalizeRenderTargetPlanName(renderTargetPlanName); // 保存归一化后的 RT 计划名，避免空字符串进入日志。

            ShouldAllocateCameraColor = shouldAllocateCameraColor; // 保存 CameraColor 分配决策。

            ShouldAllocateCameraDepth = shouldAllocateCameraDepth; // 保存 CameraDepth 分配决策。

            ShouldFinalBlit = shouldFinalBlit; // 保存 FinalBlit 决策。

            ShouldReleaseCameraColor = shouldReleaseCameraColor; // 保存 CameraColor 释放决策。

            ShouldReleaseCameraDepth = shouldReleaseCameraDepth; // 保存 CameraDepth 释放决策。
        }

        public static BurtRequestRenderOptions CreateSingleRequest() // 创建旧单相机路径的默认选项，用来保证未接入 Stack 时行为完全不变。
        {
            return new BurtRequestRenderOptions( // 返回一个“独立 request”配置，每个 request 自己申请、输出并释放 RT。
                false, // 单相机默认不共享栈级 RT。
                true, // 单相机同时也是栈内第一个 request。
                true, // 单相机同时也是栈内最后一个 request。
                0, // 单相机的栈内索引固定为 0。
                1, // 单相机的栈内数量固定为 1。
                0, // 没有明确栈时使用 0 号栈作为安全默认值。
                "SingleRequestRT", // 使用清晰的计划名表示这是旧的单 request 生命周期。
                true, // 旧路径每个 request 都申请 CameraColor。
                true, // 旧路径每个 request 都申请 CameraDepth。
                true, // 旧路径每个 request 都执行 FinalBlit。
                true, // 旧路径每个 request 都释放 CameraColor。
                true); // 旧路径每个 request 都释放 CameraDepth。
        }

        public static BurtRequestRenderOptions CreateIsolatedRequest( // 创建非共享栈里的 request 选项，例如 SceneView、Preview 或非法栈。
            int requestIndexInStack, // 接收当前 request 在栈内的索引。
            int requestCountInStack, // 接收当前栈内 request 总数。
            int stackId, // 接收当前逻辑栈编号。
            string renderTargetPlanName) // 接收当前 RT 计划名称。
        {
            var safeRequestCount = NormalizeRequestCount(requestCountInStack); // 把 request 总数修正到至少为 1。

            var safeRequestIndex = ClampRequestIndex(requestIndexInStack, safeRequestCount); // 把索引夹到合法范围内，避免异常输入影响首尾判断。

            return new BurtRequestRenderOptions( // 返回一个独立生命周期配置，让每个 request 自己完成 RT 生命周期。
                false, // 非共享路径不复用其他 request 申请的 CameraColor 或 CameraDepth。
                safeRequestIndex == 0, // 根据修正后的索引判断是否是栈内第一个 request。
                safeRequestIndex == safeRequestCount - 1, // 根据修正后的索引判断是否是栈内最后一个 request。
                safeRequestIndex, // 保存修正后的栈内索引。
                safeRequestCount, // 保存修正后的栈内 request 总数。
                stackId, // 保存逻辑相机栈编号。
                renderTargetPlanName, // 保存调用方提供的 RT 计划名称。
                true, // 独立 request 必须自己申请 CameraColor。
                true, // 独立 request 必须自己申请 CameraDepth。
                true, // 独立 request 必须自己 FinalBlit 到最终目标。
                true, // 独立 request 必须自己释放 CameraColor。
                true); // 独立 request 必须自己释放 CameraDepth。
        }

        public static BurtRequestRenderOptions CreateSharedStackRequest( // 创建共享栈级 RT 的 request 选项。
            int requestIndexInStack, // 接收当前 request 在共享栈里的索引。
            int requestCountInStack, // 接收共享栈里的 request 总数。
            int stackId, // 接收逻辑相机栈编号。
            string renderTargetPlanName) // 接收当前 RT 计划名称。
        {
            var safeRequestCount = NormalizeRequestCount(requestCountInStack); // 把 request 总数修正到至少为 1。

            var safeRequestIndex = ClampRequestIndex(requestIndexInStack, safeRequestCount); // 把索引夹到合法范围内，避免异常输入影响资源生命周期。

            var isFirstRequest = safeRequestIndex == 0; // 只有栈内第一个 request 负责申请栈级颜色和深度 RT。

            var isLastRequest = safeRequestIndex == safeRequestCount - 1; // 只有栈内最后一个 request 负责最终输出和释放栈级 RT。

            return new BurtRequestRenderOptions( // 返回一个共享 RT 配置，让 CameraColor/CameraDepth 生命周期跨多个 request。
                true, // 标记当前 request 处于共享栈级 RT 路径。
                isFirstRequest, // 保存是否为栈内第一个 request。
                isLastRequest, // 保存是否为栈内最后一个 request。
                safeRequestIndex, // 保存修正后的栈内索引。
                safeRequestCount, // 保存修正后的栈内 request 总数。
                stackId, // 保存逻辑相机栈编号。
                renderTargetPlanName, // 保存调用方提供的 RT 计划名称。
                isFirstRequest, // 共享 RT 只在第一个 request 申请 CameraColor。
                isFirstRequest, // 共享 RT 只在第一个 request 申请 CameraDepth。
                isLastRequest, // 共享 RT 只在最后一个 request 执行 FinalBlit。
                isLastRequest, // 共享 RT 只在最后一个 request 释放 CameraColor。
                isLastRequest); // 共享 RT 只在最后一个 request 释放 CameraDepth。
        }

        private static int NormalizeRequestCount(int requestCountInStack) // 把 request 数量修正为安全值。
        {
            return requestCountInStack > 0 ? requestCountInStack : 1; // request 数量至少为 1，避免后续计算最后索引时出现负数。
        }

        private static int ClampRequestIndex( // 把 request 索引夹到合法范围内。
            int requestIndexInStack, // 接收原始 request 索引。
            int requestCountInStack) // 接收已经修正后的 request 总数。
        {
            if (requestIndexInStack < 0) // 如果索引小于 0，说明调用方传入了异常值。
            {
                return 0; // 使用第一个 request 作为安全兜底。
            }

            if (requestIndexInStack >= requestCountInStack) // 如果索引超过最后一个合法位置，说明调用方传入了异常值。
            {
                return requestCountInStack - 1; // 使用最后一个 request 作为安全兜底。
            }

            return requestIndexInStack; // 索引本身合法时直接返回原值。
        }

        private static string NormalizeRenderTargetPlanName(string renderTargetPlanName) // 归一化 RT 计划名称。
        {
            return string.IsNullOrEmpty(renderTargetPlanName) ? "UnknownRTPlan" : renderTargetPlanName; // 空计划名统一显示为 UnknownRTPlan，方便诊断。
        }
    }
}
