using System.Collections.Generic; // 引入泛型集合命名空间，用来使用 List 保存资源读写列表。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让资源使用信息和其他 BurtRP 代码处在同一个模块里。
{
    public sealed class BurtRenderPassResourceUsage // 定义单个 RenderPass 的资源使用记录，用来描述这个 Pass 读写了哪些资源。
    {
        private readonly List<BurtRenderTargetHandle> readRenderTargets = new List<BurtRenderTargetHandle>(); // 保存这个 Pass 声明读取的所有渲染目标句柄。

        private readonly List<BurtRenderTargetHandle> writeRenderTargets = new List<BurtRenderTargetHandle>(); // 保存这个 Pass 声明写入的所有渲染目标句柄。

        public string PassName { get; } // 保存这个资源使用记录对应的 Pass 名称，方便调试和日志输出。

        public IReadOnlyList<BurtRenderTargetHandle> ReadRenderTargets => readRenderTargets; // 暴露只读的读取资源列表，避免外部直接修改内部 List。

        public IReadOnlyList<BurtRenderTargetHandle> WriteRenderTargets => writeRenderTargets; // 暴露只读的写入资源列表，避免外部直接修改内部 List。

        public BurtRenderPassResourceUsage(string passName) // 定义构造函数，用来创建一个 Pass 的资源使用记录。
        {
            PassName = string.IsNullOrEmpty(passName) ? "UnnamedPass" : passName; // 如果 Pass 名称为空，就使用兜底名称，避免调试信息缺失。
        }

        public void AddReadRenderTarget(BurtRenderTargetHandle handle) // 定义记录读取渲染目标的函数。
        {
            readRenderTargets.Add(handle); // 把传入的渲染目标句柄加入读取列表，当前阶段先不做去重。
        }

        public void AddWriteRenderTarget(BurtRenderTargetHandle handle) // 定义记录写入渲染目标的函数。
        {
            writeRenderTargets.Add(handle); // 把传入的渲染目标句柄加入写入列表，当前阶段先不做去重。
        }
    }
}
