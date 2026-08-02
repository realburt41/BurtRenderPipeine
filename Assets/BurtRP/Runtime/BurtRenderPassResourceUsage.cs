using System.Collections.Generic; // 引入泛型集合命名空间，用来保存资源读写列表和轻量校验消息。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让资源使用信息和其他 BurtRP 代码处在同一个模块里。
{
    public enum BurtRenderResourceAccessType // Typed resource access used by RenderGraph debug output.
    {
        Read,
        Write,
        Allocate,
        Bind,
        Clear,
        Release,
        Copy,
    }

    public readonly struct BurtRenderTargetResourceAccess // Stores one typed render-target access declaration.
    {
        public BurtRenderTargetResourceAccess(BurtRenderTargetHandle handle, BurtRenderResourceAccessType accessType)
        {
            Handle = handle;
            AccessType = accessType;
        }

        public BurtRenderTargetHandle Handle { get; }

        public BurtRenderResourceAccessType AccessType { get; }
    }

    public readonly struct BurtRenderBufferResourceAccess // Stores one typed logical-buffer access declaration.
    {
        public BurtRenderBufferResourceAccess(BurtRenderBufferHandle handle, BurtRenderResourceAccessType accessType)
        {
            Handle = handle;
            AccessType = accessType;
        }

        public BurtRenderBufferHandle Handle { get; }

        public BurtRenderResourceAccessType AccessType { get; }
    }

    public readonly struct BurtGlobalResourceAccess // Stores one typed logical global-resource access declaration.
    {
        public BurtGlobalResourceAccess(string resourceName, BurtRenderResourceAccessType accessType)
        {
            ResourceName = string.IsNullOrEmpty(resourceName) ? string.Empty : resourceName;
            AccessType = accessType;
        }

        public string ResourceName { get; }

        public BurtRenderResourceAccessType AccessType { get; }
    }

    public sealed class BurtRenderPassResourceUsage // 定义单个 RenderPass 的资源使用记录，用来描述这个 Pass 读写了哪些资源。
    {
        private readonly List<BurtRenderTargetHandle> readRenderTargets = new List<BurtRenderTargetHandle>(); // 保存这个 Pass 声明读取的所有渲染目标句柄。

        private readonly List<BurtRenderTargetHandle> writeRenderTargets = new List<BurtRenderTargetHandle>(); // 保存这个 Pass 声明写入的所有渲染目标句柄。

        private readonly List<BurtRenderTargetResourceAccess> renderTargetAccesses = new List<BurtRenderTargetResourceAccess>(); // Typed RT accesses for debug and future culling.

        private readonly List<BurtRenderBufferHandle> readBuffers = new List<BurtRenderBufferHandle>(); // Logical buffers read by this pass.

        private readonly List<BurtRenderBufferHandle> writeBuffers = new List<BurtRenderBufferHandle>(); // Logical buffers written by this pass.

        private readonly List<BurtRenderBufferResourceAccess> bufferAccesses = new List<BurtRenderBufferResourceAccess>(); // Typed buffer accesses for debug and future tiled/cluster passes.

        private readonly List<BurtGlobalResourceAccess> globalResourceAccesses = new List<BurtGlobalResourceAccess>(); // Typed global-resource accesses for debug.

        private readonly List<string> readGlobalResources = new List<string>(); // 保存这个 Pass 声明读取的逻辑全局资源，例如 LightingGlobals。

        private readonly List<string> writeGlobalResources = new List<string>(); // 保存这个 Pass 声明写入的逻辑全局资源，例如 ShadowGlobals。

        private readonly List<string> validationMessages = new List<string>(); // 保存配置阶段发现的本 Pass 资源声明问题，只用于 Debug/Validation 输出。

        private readonly List<string> allowUnconsumedWriteResources = new List<string>(); // Resources that are intentionally terminal side-effect writes.

        public int PassIndex { get; } // 保存这个资源使用记录对应的 Pass 顺序，方便日志和实际执行顺序对齐。

        public string PassName { get; } // 保存这个资源使用记录对应的 Pass 名称，方便调试和日志输出。

        public BurtRenderPassKind PassKind { get; } // Pass category for debug output and future scheduling analysis.

        public bool HasSideEffects { get; } // Conservative side-effect marker; not used for culling yet.

        public bool AllowCulling { get; } // True when the compiler may omit this pass if none of its outputs are live.

        public bool EnableAsyncCompute { get; }

        public IReadOnlyList<BurtRenderTargetHandle> ReadRenderTargets => readRenderTargets; // 暴露只读的读取资源列表，避免外部直接修改内部 List。

        public IReadOnlyList<BurtRenderTargetHandle> WriteRenderTargets => writeRenderTargets; // 暴露只读的写入资源列表，避免外部直接修改内部 List。

        public IReadOnlyList<BurtRenderTargetResourceAccess> RenderTargetAccesses => renderTargetAccesses; // Typed RT access list for RenderGraph debug output.

        public IReadOnlyList<BurtRenderBufferHandle> ReadBuffers => readBuffers; // Logical buffer read declarations.

        public IReadOnlyList<BurtRenderBufferHandle> WriteBuffers => writeBuffers; // Logical buffer write declarations.

        public IReadOnlyList<BurtRenderBufferResourceAccess> BufferAccesses => bufferAccesses; // Typed buffer access list for RenderGraph debug output.

        public IReadOnlyList<BurtGlobalResourceAccess> GlobalResourceAccesses => globalResourceAccesses; // Typed global-resource access list for RenderGraph debug output.

        public IReadOnlyList<string> ReadGlobalResources => readGlobalResources; // 暴露只读的逻辑全局资源读取列表，供 RenderGraph Debug 和 Validation 使用。

        public IReadOnlyList<string> WriteGlobalResources => writeGlobalResources; // 暴露只读的逻辑全局资源写入列表，供 RenderGraph Debug 和 Validation 使用。

        public IReadOnlyList<string> AllowUnconsumedWriteResources => allowUnconsumedWriteResources; // Exposes terminal-write exceptions for validation/debug only.

        public IReadOnlyList<string> ValidationMessages => validationMessages; // 暴露只读校验消息，让 RenderGraph dump 可以集中展示问题。

        public bool HasResourceDeclarations => readRenderTargets.Count > 0 || writeRenderTargets.Count > 0 || readBuffers.Count > 0 || writeBuffers.Count > 0 || readGlobalResources.Count > 0 || writeGlobalResources.Count > 0; // 标记这个 Pass 是否声明了任意渲染目标、Buffer 或逻辑全局资源依赖。

        public BurtRenderPassResourceUsage(string passName) // Backward-compatible constructor for older callers.
            : this(-1, passName, BurtRenderPassKindUtility.InferKind(passName), true, false)
        {
        }

        public BurtRenderPassResourceUsage( // Creates a pass resource usage record with conservative metadata defaults.
            int passIndex,
            string passName)
            : this(passIndex, passName, BurtRenderPassKindUtility.InferKind(passName), true, false)
        {
        }

        public BurtRenderPassResourceUsage( // Creates a pass resource usage record with explicit metadata.
            int passIndex,
            string passName,
            BurtRenderPassKind passKind,
            bool hasSideEffects,
            bool allowCulling,
            bool enableAsyncCompute = false)
        {
            PassIndex = passIndex; // Store the graph order for stable debug labels.

            PassName = string.IsNullOrEmpty(passName) ? "UnnamedPass" : passName; // Keep debug output readable when a pass name is missing.

            PassKind = passKind; // Store pass category for debug output.

            HasSideEffects = hasSideEffects; // Store conservative side-effect metadata.

            AllowCulling = allowCulling; // Store the audited culling contract used by the graph compiler.

            EnableAsyncCompute = enableAsyncCompute;
        }

        public void AddReadRenderTarget(BurtRenderTargetHandle handle) // 定义记录读取渲染目标的函数。
        {
            ValidateRenderTargetHandle(handle, "Read"); // 在不改变声明结果的前提下记录空名或无效句柄问题。

            if (ContainsRenderTarget(readRenderTargets, handle.Name)) // 如果同一 Pass 重复声明读取同一个资源，就记录诊断信息。
            {
                AddValidationMessage("重复 Read 声明: " + FormatResourceName(handle.Name)); // 重复声明不阻断渲染，只在 Debug 中提示。
            }

            readRenderTargets.Add(handle); // 把传入的渲染目标句柄加入读取列表，保留原始声明顺序便于排查。

            renderTargetAccesses.Add(new BurtRenderTargetResourceAccess(handle, ResolveReadAccessType())); // Preserve Read list while recording Release semantics for release passes.
        }

        public void AddWriteRenderTarget(BurtRenderTargetHandle handle) // 定义记录写入渲染目标的函数。
        {
            ValidateRenderTargetHandle(handle, "Write"); // 在不改变声明结果的前提下记录空名或无效句柄问题。

            if (ContainsRenderTarget(writeRenderTargets, handle.Name)) // 如果同一 Pass 重复声明写入同一个资源，就记录诊断信息。
            {
                AddValidationMessage("重复 Write 声明: " + FormatResourceName(handle.Name)); // 重复写入声明保留原样，但在 Debug 中提示。
            }

            writeRenderTargets.Add(handle); // 把传入的渲染目标句柄加入写入列表，保留原始声明顺序便于排查。

            renderTargetAccesses.Add(new BurtRenderTargetResourceAccess(handle, ResolveWriteAccessType())); // Preserve Write list while recording Allocate/Bind/Clear/Copy semantics.
        }

        public void AddReadBuffer(BurtRenderBufferHandle handle) // Records a logical buffer read declaration.
        {
            ValidateBufferHandle(handle, "Read Buffer");

            if (ContainsBuffer(readBuffers, handle.Name))
            {
                AddValidationMessage("重复 Read Buffer 声明: " + FormatResourceName(handle.Name));
            }

            readBuffers.Add(handle);

            bufferAccesses.Add(new BurtRenderBufferResourceAccess(handle, ResolveReadAccessType()));
        }

        public void AddWriteBuffer(BurtRenderBufferHandle handle) // Records a logical buffer write declaration.
        {
            ValidateBufferHandle(handle, "Write Buffer");

            if (ContainsBuffer(writeBuffers, handle.Name))
            {
                AddValidationMessage("重复 Write Buffer 声明: " + FormatResourceName(handle.Name));
            }

            writeBuffers.Add(handle);

            bufferAccesses.Add(new BurtRenderBufferResourceAccess(handle, ResolveWriteAccessType()));
        }

        public void AddReadGlobalResource(string resourceName) // 定义记录读取逻辑全局资源的函数。
        {
            var safeName = ValidateGlobalResourceName(resourceName, "Read Global"); // 校验并归一化全局资源名，避免空名进入依赖图。

            if (string.IsNullOrEmpty(safeName)) // 如果资源名无效，说明已经记录过校验消息。
            {
                return; // 直接返回，避免把空资源写入列表污染 Debug 输出。
            }

            if (readGlobalResources.Contains(safeName)) // 如果同一 Pass 重复声明读取同一个全局资源，就记录诊断信息。
            {
                AddValidationMessage("重复 Read Global 声明: " + safeName); // 重复声明不阻断渲染，只在 Debug 中提示。
            }

            readGlobalResources.Add(safeName); // 把全局资源名加入读取列表，保留原始声明顺序便于排查。

            globalResourceAccesses.Add(new BurtGlobalResourceAccess(safeName, BurtRenderResourceAccessType.Read)); // Record typed global read access.
        }

        public void AddWriteGlobalResource(string resourceName) // 定义记录写入逻辑全局资源的函数。
        {
            var safeName = ValidateGlobalResourceName(resourceName, "Write Global"); // 校验并归一化全局资源名，避免空名进入依赖图。

            if (string.IsNullOrEmpty(safeName)) // 如果资源名无效，说明已经记录过校验消息。
            {
                return; // 直接返回，避免把空资源写入列表污染 Debug 输出。
            }

            if (writeGlobalResources.Contains(safeName)) // 如果同一 Pass 重复声明写入同一个全局资源，就记录诊断信息。
            {
                AddValidationMessage("重复 Write Global 声明: " + safeName); // 重复声明不阻断渲染，只在 Debug 中提示。
            }

            writeGlobalResources.Add(safeName); // 把全局资源名加入写入列表，保留原始声明顺序便于排查。

            globalResourceAccesses.Add(new BurtGlobalResourceAccess(safeName, BurtRenderResourceAccessType.Write)); // Record typed global write access.
        }

        public void AllowUnconsumedWriteResource(string resourceName) // Marks a declared write as an intentional terminal side effect.
        {
            var safeName = ValidateTerminalWriteResourceName(resourceName); // Normalize and validate before storing debug metadata.
            if (string.IsNullOrEmpty(safeName))
            {
                return;
            }

            if (allowUnconsumedWriteResources.Contains(safeName))
            {
                AddValidationMessage("重复 AllowUnconsumedWrite 标记: " + safeName);
                return;
            }

            allowUnconsumedWriteResources.Add(safeName);
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

        private BurtRenderResourceAccessType ResolveReadAccessType() // Maps legacy Read declarations to typed resource semantics.
        {
            return PassKind == BurtRenderPassKind.Release ? BurtRenderResourceAccessType.Release : BurtRenderResourceAccessType.Read;
        }

        private BurtRenderResourceAccessType ResolveWriteAccessType() // Maps legacy Write declarations to typed resource semantics.
        {
            switch (PassKind)
            {
                case BurtRenderPassKind.Allocate:
                    return BurtRenderResourceAccessType.Allocate;
                case BurtRenderPassKind.Release:
                    return BurtRenderResourceAccessType.Release;
                case BurtRenderPassKind.SetRenderTarget:
                    return BurtRenderResourceAccessType.Bind;
                case BurtRenderPassKind.Clear:
                    return BurtRenderResourceAccessType.Clear;
                case BurtRenderPassKind.Copy:
                    return BurtRenderResourceAccessType.Copy;
                default:
                    return BurtRenderResourceAccessType.Write;
            }
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

        private void ValidateBufferHandle(BurtRenderBufferHandle handle, string accessType) // Validates a logical buffer handle for dependency tracking.
        {
            if (string.IsNullOrEmpty(handle.Name))
            {
                AddValidationMessage(accessType + " 声明使用空 Buffer 资源名。");
            }

            if (!handle.IsValid)
            {
                AddValidationMessage(accessType + " 声明引用缺失 Buffer: " + FormatResourceName(handle.Name));
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

        private static bool ContainsBuffer(IReadOnlyList<BurtRenderBufferHandle> handles, string resourceName) // Checks whether a buffer list already contains the same logical name.
        {
            for (var handleIndex = 0; handleIndex < handles.Count; handleIndex++)
            {
                if (handles[handleIndex].Name == resourceName)
                {
                    return true;
                }
            }

            return false;
        }

        private static string FormatResourceName(string resourceName) // 把资源名转换成适合日志显示的文本。
        {
            return string.IsNullOrEmpty(resourceName) ? "<empty>" : resourceName; // 空资源名使用醒目的占位符。
        }

        private string ValidateGlobalResourceName( // 校验逻辑全局资源名，并返回可用于列表记录的安全名称。
            string resourceName, // 接收调用方声明的逻辑全局资源名。
            string accessType) // 接收访问类型，例如 Read Global 或 Write Global。
        {
            if (string.IsNullOrEmpty(resourceName)) // 如果资源名为空，依赖图无法追踪这个全局状态。
            {
                AddValidationMessage(accessType + " 声明使用空全局资源名。"); // 记录空资源名问题，提示调用方补齐名称。

                return string.Empty; // 返回空字符串，让调用方跳过列表写入。
            }

            return resourceName; // 返回原始资源名，保持 Debug 输出和调用方声明一致。
        }

        private string ValidateTerminalWriteResourceName(string resourceName) // Validates an intentional terminal-write marker name.
        {
            if (string.IsNullOrEmpty(resourceName))
            {
                AddValidationMessage("AllowUnconsumedWrite 标记使用空资源名。");

                return string.Empty;
            }

            return resourceName;
        }
    }
}
