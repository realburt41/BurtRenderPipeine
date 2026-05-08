namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让 Builder 和其他 BurtRP 代码处在同一个模块里。
{
    public sealed class BurtRenderPassBuilder // 定义 RenderPass 配置阶段使用的 Builder，用来让 Pass 声明资源读写关系。
    {
        public BurtRenderGraphResourceRegistry ResourceRegistry { get; } // 保存当前 RenderGraph 的资源注册表，Builder 通过它查找资源句柄。

        public BurtRenderPassResourceUsage Usage { get; } // 保存当前 Pass 的资源使用记录，RenderGraph 会在配置阶段收集它。

        public BurtRenderPassBuilder( // 定义构造函数，用来为某个 Pass 创建资源声明 Builder。
            BurtRenderPass pass, // 接收正在配置的 RenderPass，用它的名称创建资源使用记录。
            BurtRenderGraphResourceRegistry resourceRegistry) // 接收当前 RenderGraph 的资源注册表。
        {
            ResourceRegistry = resourceRegistry; // 把资源注册表保存到 ResourceRegistry 属性里。

            var passName = pass != null ? pass.Name : "NullPass"; // 如果 Pass 存在就读取它的名称，否则使用空 Pass 兜底名称。

            Usage = new BurtRenderPassResourceUsage(passName); // 为当前 Pass 创建一份资源使用记录。
        }

        public BurtRenderTargetHandle ReadRenderTarget(string name) // 定义声明读取某个渲染目标资源的函数。
        {
            var handle = GetRenderTarget(name); // 从资源注册表里读取指定名称的渲染目标句柄。

            Usage.AddReadRenderTarget(handle); // 把这个句柄记录为当前 Pass 的读取资源。

            return handle; // 返回这个句柄，方便 Pass 后续需要时继续使用。
        }

        public BurtRenderTargetHandle WriteRenderTarget(string name) // 定义声明写入某个渲染目标资源的函数。
        {
            var handle = GetRenderTarget(name); // 从资源注册表里读取指定名称的渲染目标句柄。

            Usage.AddWriteRenderTarget(handle); // 把这个句柄记录为当前 Pass 的写入资源。

            return handle; // 返回这个句柄，方便 Pass 后续需要时继续使用。
        }

        public BurtRenderTargetHandle ReadCameraColor() // 定义声明读取 CameraColor 的快捷函数。
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.CameraColorName); // 使用统一资源名声明读取 CameraColor。
        }

        public BurtRenderTargetHandle WriteCameraColor() // 定义声明写入 CameraColor 的快捷函数。
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.CameraColorName); // 使用统一资源名声明写入 CameraColor。
        }

        private BurtRenderTargetHandle GetRenderTarget(string name) // 定义从资源表读取渲染目标的内部辅助函数。
        {
            if (ResourceRegistry == null) // 如果资源注册表为空，说明当前 Builder 没有可查询的资源来源。
            {
                return BurtRenderTargetHandle.Invalid(name); // 返回无效句柄，让使用记录保留资源名但不绑定真实目标。
            }

            return ResourceRegistry.GetRenderTarget(name); // 从资源注册表读取指定名称的渲染目标句柄。
        }
    }
}
