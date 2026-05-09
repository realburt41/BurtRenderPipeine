using System.Text; // 引入文本构建命名空间，让前缀工具可以把统一标签追加到已有 StringBuilder。

namespace Burt.RenderPipeline // 定义 BurtRP 的运行时命名空间，保证调试工具和渲染管线核心类型位于同一个模块。
{
    internal static class BurtDebugLogUtility // 定义统一调试日志格式工具，只负责拼接文本，不主动写入 Unity Console。
    {
        public const string PipelinePrefix = "[BurtRP]"; // 定义管线总前缀，所有 BurtRP 调试文本都应带上它，方便 Console 搜索和过滤。

        public const string RenderGraphPrefix = "[BurtRenderGraph]"; // 定义 RenderGraph 子系统前缀，专门标记渲染图 dump 和资源依赖信息。

        public const string CameraPrefix = "[BurtCamera]"; // 定义 Camera 子系统前缀，给相机分类、排序、请求调试输出预留统一标签。

        public const string ShadowPrefix = "[BurtShadow]"; // 定义 Shadow 子系统前缀，给阴影诊断日志预留统一标签，避免每个文件自己写字符串。

        public static string BuildScopedPrefix(string subsystemPrefix) // 根据子系统前缀生成完整日志前缀，例如 [BurtRP][BurtRenderGraph]。
        {
            if (string.IsNullOrEmpty(subsystemPrefix)) // 如果调用方没有传入子系统前缀，就只返回管线总前缀。
            {
                return PipelinePrefix; // 返回 [BurtRP]，让日志至少能被总管线标签搜索到。
            }

            return PipelinePrefix + subsystemPrefix; // 把总前缀和子系统前缀拼在一起，保持所有日志的标签顺序一致。
        }

        public static void AppendScopedPrefix( // 把完整日志前缀追加到已有 StringBuilder，避免调用方反复创建短字符串。
            StringBuilder builder, // 接收调用方正在使用的字符串构建器，可能来自调试 StringBuilder 池。
            string subsystemPrefix) // 接收子系统前缀，建议使用本类提供的 RenderGraphPrefix、CameraPrefix 或 ShadowPrefix。
        {
            if (builder == null) // 如果调用方传入空构建器，说明没有地方可以写入前缀。
            {
                return; // 直接返回，避免调试工具因为空引用影响真实渲染流程。
            }

            builder.Append(PipelinePrefix); // 先追加管线总前缀，保证所有 BurtRP 日志都能用 [BurtRP] 统一检索。

            if (!string.IsNullOrEmpty(subsystemPrefix)) // 如果存在具体子系统前缀，就继续补充更细的分类标签。
            {
                builder.Append(subsystemPrefix); // 追加子系统前缀，例如 [BurtRenderGraph]，便于快速定位日志来源。
            }
        }

        public static void AppendScopedHeaderLine( // 把完整前缀作为一行标题写入 StringBuilder。
            StringBuilder builder, // 接收调用方正在写入的字符串构建器。
            string subsystemPrefix) // 接收要写入标题的子系统前缀。
        {
            if (builder == null) // 如果构建器为空，就没有安全写入目标。
            {
                return; // 直接返回，保持工具函数空值安全。
            }

            AppendScopedPrefix(builder, subsystemPrefix); // 复用统一前缀拼接逻辑，避免标题格式和普通消息格式分叉。

            builder.AppendLine(); // 在前缀后换行，让后续多行 dump 从下一行开始，Console 可读性更好。
        }

        public static string FormatMessage( // 生成带统一前缀的单行消息，供未来日志入口在已有开关控制下使用。
            string subsystemPrefix, // 接收子系统前缀，用来标记消息来源。
            string message) // 接收实际消息正文，工具会对空字符串做安全兜底。
        {
            var messageLength = !string.IsNullOrEmpty(message) ? message.Length : 7; // 估算消息长度；空消息会写入 <empty>，长度按 7 预留。

            var builder = BurtDebugStringBuilderPool.Get(PipelinePrefix.Length + messageLength + 16); // 从调试 StringBuilder 池取一个小构建器，减少频繁格式化时的 GC。

            try // 使用 try/finally 保证即使未来追加逻辑出错也会归还构建器。
            {
                AppendScopedPrefix(builder, subsystemPrefix); // 先写统一前缀，保持单行消息和多行 dump 的标签一致。

                builder.Append(' '); // 在前缀和正文之间插入空格，让日志更容易阅读。

                builder.Append(string.IsNullOrEmpty(message) ? "<empty>" : message); // 写入消息正文；没有正文时写入占位，避免产生只有前缀的日志。

                return builder.ToString(); // 把构建好的单行文本返回给调用方，由调用方决定是否 Debug.Log。
            }
            finally // 不管返回路径如何，都把临时构建器还给池。
            {
                BurtDebugStringBuilderPool.Release(builder); // 释放构建器引用，避免调试格式化工具自己制造持续分配。
            }
        }

        public static void AppendKeyValueLine( // 把常见的 Key: Value 诊断字段写成一行，减少各个 debug dump 自己拼格式。
            StringBuilder builder, // 接收正在写入的字符串构建器。
            string key, // 接收字段名称，例如 Request、Camera 或 Pass Count。
            object value) // 接收字段值，允许传入枚举、数字、字符串或 null。
        {
            if (builder == null) // 如果构建器为空，就不能安全写入。
            {
                return; // 直接返回，避免辅助格式化影响渲染流程。
            }

            builder.Append(string.IsNullOrEmpty(key) ? "<key>" : key); // 写入字段名；字段名为空时写占位，方便发现调用错误。

            builder.Append(": "); // 写入统一的键值分隔符，让 dump 中的字段对齐成同一种阅读习惯。

            builder.Append(value != null ? value.ToString() : "null"); // 写入字段值；值为空时显式写 null，避免看起来像漏了一列。

            builder.AppendLine(); // 当前键值字段结束后换行，让下一条字段独占一行。
        }
    }
}