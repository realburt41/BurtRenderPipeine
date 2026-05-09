using System.Collections.Generic; // 引入泛型集合命名空间，用 Stack 保存少量可复用的 StringBuilder。
using System.Text; // 引入文本构建命名空间，用来创建和复用 StringBuilder 实例。

namespace Burt.RenderPipeline // 定义 BurtRP 的运行时命名空间，让调试池能被同程序集内的工具直接使用。
{
    internal static class BurtDebugStringBuilderPool // 定义调试专用 StringBuilder 池，减少 dump 字符串拼接过程中的临时分配。
    {
        private const int DefaultCapacity = 256; // 定义默认容量，覆盖短日志和小型 dump，避免每次都从很小容量开始扩容。

        private const int MaxRetainedCapacity = 8192; // 定义允许缓存的最大容量，避免一次超大 dump 让池长期持有大数组。

        private const int MaxRetainedBuilders = 4; // 定义池里最多保留几个构建器，调试日志通常在主线程串行生成，不需要更大的池。

        private static readonly Stack<StringBuilder> Pool = new Stack<StringBuilder>(MaxRetainedBuilders); // 保存可复用构建器实例，使用 Stack 让最近用过的对象优先复用。

        public static StringBuilder Get(int capacity = DefaultCapacity) // 从池里取一个 StringBuilder，并保证它至少有调用方需要的容量。
        {
            var safeCapacity = capacity > 0 ? capacity : DefaultCapacity; // 修正非法容量，避免调用方传入 0 或负数导致后续构造异常。

            lock (Pool) // 锁住共享池，防止未来有多线程调试入口时同时 Pop 造成集合状态异常。
            {
                if (Pool.Count > 0) // 如果池里已经有可复用构建器，就优先复用旧对象。
                {
                    var builder = Pool.Pop(); // 取出最近归还的构建器，减少重新分配托管对象。

                    builder.Length = 0; // 清空旧文本内容，保证新的日志不会带上上一条 dump 的残留。

                    if (builder.Capacity < safeCapacity) // 如果旧构建器容量不足，就扩到调用方需要的容量。
                    {
                        builder.EnsureCapacity(safeCapacity); // 预留足够空间，减少后续 Append 过程中的多次扩容。
                    }

                    return builder; // 返回已经清空并确保容量的构建器给调用方使用。
                }
            }

            return new StringBuilder(safeCapacity); // 池为空时创建新构建器，只在没有可复用对象时产生分配。
        }

        public static string ToStringAndRelease(StringBuilder builder) // 把构建器内容转成字符串并立即归还池，简化简单调用场景。
        {
            if (builder == null) // 如果调用方传入空构建器，就没有内容可以转换。
            {
                return string.Empty; // 返回空字符串，保持工具函数空值安全。
            }

            var text = builder.ToString(); // 先复制出最终字符串，因为归还池以后构建器内容会被清空。

            Release(builder); // 把构建器归还池，允许后续调试格式化继续复用它。

            return text; // 返回已经独立拷贝出来的字符串给调用方输出或保存。
        }

        public static void Release(StringBuilder builder) // 把用完的 StringBuilder 归还池，或者在容量过大时丢弃。
        {
            if (builder == null) // 如果传入空引用，说明调用方没有实际借出构建器。
            {
                return; // 直接返回，避免空引用异常。
            }

            builder.Length = 0; // 归还前清空文本，保证池内对象不保留上一条日志内容。

            if (builder.Capacity > MaxRetainedCapacity) // 如果构建器内部数组太大，就不缓存它。
            {
                return; // 直接丢弃大构建器，让 GC 回收，避免调试工具常驻占用过多内存。
            }

            lock (Pool) // 锁住共享池，保证 Count 检查和 Push 操作是连续的。
            {
                if (Pool.Count >= MaxRetainedBuilders) // 如果池已经满了，就没有必要继续缓存更多对象。
                {
                    return; // 直接丢弃多余构建器，保持池容量上限稳定。
                }

                Pool.Push(builder); // 把清空后的构建器放回池中，等待下一次 debug dump 复用。
            }
        }
    }
}