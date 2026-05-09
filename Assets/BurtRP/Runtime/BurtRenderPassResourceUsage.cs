using System.Collections.Generic; // 引入泛型集合命名空间，用来保存资源读写列表和轻量校验消息。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让资源使用信息和其他 BurtRP 代码处在同一个模块里。
{
    public sealed class BurtRenderPassResourceUsage // 定义单个 RenderPass 的资源使用记录，用来描述这个 Pass 读写了哪些资源。
    {
        private readonly List<BurtRenderTargetHandle> readRenderTargets = new List<BurtRenderTargetHandle>(); // 保存这个 Pass 声明读取的所有渲染目标句柄。

        private readonly List<BurtRenderTargetHandle> writeRenderTargets = new List<BurtRenderTargetHandle>(); // 保存这个 Pass 声明写入的所有渲染目标句柄。

        private readonly List<string> validationMessages = new List<string>(); // 保存配置阶段发现的本 Pass 资源声明问题，只用于 Debug/Validation 输出。

        public int PassIndex { get; } // 保存这个资源使用记录对应的 Pass 顺序，方便日志和实际执行顺序对齐。

        public string PassName { get; } // 保存这个资源使用记录对应的 Pass 名称，方便调试和日志输出。

        public IReadOnlyList<BurtRenderTargetHandle> ReadRenderTargets => readRenderTargets; // 暴露只读的读取资源列表，避免外部直接修改内部 List。

        public IReadOnlyList<BurtRenderTargetHandle> WriteRenderTargets => writeRenderTargets; // 暴露只读的写入资源列表，避免外部直接修改内部 List。

        public IReadOnlyList<string> ValidationMessages => validationMessages; // 暴露只读校验消息，让 RenderGraph dump 可以集中展示问题。

        public bool HasResourceDeclarations => readRenderTargets.Count > 0 || writeRenderTargets.Count > 0; // 标记这个 Pass 是否声明了任意资源依赖。

        public BurtRenderPassResourceUsage(string passName) // 保留旧构造函数，避免已有调用方因为新增 PassIndex 而失效。
            : this(-1, passName)
        {
        }

        public BurtRenderPassResourceUsage( // 定义构造函数，用来创建一个 Pass 的资源使用记录。
            int passIndex, // 接收 Pass 在 RenderGraph 中的顺序索引。
            string passName) // 接收 Pass 名称，可能为空。
        {
            PassIndex = passIndex; // 保存 Pass 索引，Debug 输出可直接定位顺序问题。

            PassName = string.IsNullOrEmpty(passName) ? "UnnamedPass" : passName; // 如果 Pass 名称为空，就使用兜底名称，避免调试信息缺失。
        }

        public void AddReadRenderTarget(BurtRenderTargetHandle handle) // 定义记录读取渲染目标的函数。
        {
            ValidateRenderTargetHandle(handle, "Read"); // 在不改变声明结果的前提下记录空名或无效句柄问题。

            if (ContainsRenderTarget(readRenderTargets, handle.Name)) // 如果同一 Pass 重复声明读取同一个资源，就记录诊断信息。
            {
                AddValidationMessage("重复 Read 声明: " + FormatResourceName(handle.Name)); // 重复声明不阻断渲染，只在 Debug 中提示。
            }

            readRenderTargets.Add(handle); // 把传入的渲染目标句柄加入读取列表，保留原始声明顺序便于排查。
        }

        public void AddWriteRenderTarget(BurtRenderTargetHandle handle) // 定义记录写入渲染目标的函数。
        {
            ValidateRenderTargetHandle(handle, "Write"); // 在不改变声明结果的前提下记录空名或无效句柄问题。

            if (ContainsRenderTarget(writeRenderTargets, handle.Name)) // 如果同一 Pass 重复声明写入同一个资源，就记录诊断信息。
            {
                AddValidationMessage("重复 Write 声明: " + FormatResourceName(handle.Name)); // 重复写入声明保留原样，但在 Debug 中提示。
            }

            writeRenderTargets.Add(handle); // 把传入的渲染目标句柄加入写入列表，保留原始声明顺序便于排查。
        }

        public void AddValidationMessage(string message) // 定义追加校验消息的函数，供 Builder 和 RenderGraph 写入配置异常。
        {
            if (string.IsNullOrEmpty(message)) // 如果消息为空，就没有可读诊断价值。
            {
                return; // 直接忽略空消息，避免 dump 中出现空行。
            }

            if (validationMessages.Contains(message)) // 同一 Pass 内相同问题只保留一次，减少每帧 Debug 输出噪音。
            {
                return; // 已存在相同消息时跳过。
            }

            validationMessages.Add(message); // 保存诊断消息，后续由 DebugUtility 统一输出。
        }

        private void ValidateRenderTargetHandle( // 校验单个资源句柄的基础可读性。
            BurtRenderTargetHandle handle, // 接收要检查的资源句柄。
            string accessType) // 接收访问类型，例如 Read 或 Write。
        {
            if (string.IsNullOrEmpty(handle.Name)) // 资源名为空会让依赖图无法被可靠追踪。
            {
                AddValidationMessage(accessType + " 声明使用空资源名。"); // 记录空资源名问题，提示调用方补齐名称。
            }

            if (!handle.IsValid) // 无效句柄通常表示资源未注册或名称写错。
            {
                AddValidationMessage(accessType + " 声明引用缺失资源: " + FormatResourceName(handle.Name)); // 记录缺失资源，帮助定位资源注册遗漏。
            }
        }

        private static bool ContainsRenderTarget( // 判断列表里是否已经声明过同名资源。
            IReadOnlyList<BurtRenderTargetHandle> handles, // 接收待扫描的资源句柄列表。
            string resourceName) // 接收要查找的资源名。
        {
            for (var handleIndex = 0; handleIndex < handles.Count; handleIndex++) // 遍历已有声明，保持兼容旧运行时不依赖 LINQ。
            {
                if (handles[handleIndex].Name == resourceName) // 资源名一致就认为是重复声明。
                {
                    return true; // 找到重复项。
                }
            }

            return false; // 没有找到同名资源。
        }

        private static string FormatResourceName(string resourceName) // 把资源名转换成适合日志显示的文本。
        {
            return string.IsNullOrEmpty(resourceName) ? "<empty>" : resourceName; // 空资源名使用醒目的占位符。
        }
    }
}
