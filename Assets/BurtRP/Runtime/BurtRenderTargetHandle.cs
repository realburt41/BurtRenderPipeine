using UnityEngine; // Provides GraphicsBuffer for RenderGraph buffer resources.
using UnityEngine.Rendering; // 引入 Unity 渲染命名空间，用来使用 RenderTargetIdentifier。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让资源句柄和其他 BurtRP 代码处在同一个模块里。
{
    public readonly struct BurtRenderTargetHandle // 定义 BurtRP 的渲染目标句柄，用来给 RenderTargetIdentifier 包一层语义名称。
    {
        public string Name { get; } // 保存这个渲染目标在 RenderGraph 里的逻辑名称，例如 CameraColor。

        public RenderTargetIdentifier Identifier { get; } // 保存 Unity 实际用于绑定渲染目标的 RenderTargetIdentifier。

        public bool IsValid { get; } // 保存这个句柄是否有效，避免 Pass 使用无效渲染目标。

        public BurtRenderTargetHandle( // 定义公开构造函数，用来创建一个有效的渲染目标句柄。
            string name, // 接收渲染目标的逻辑名称。
            RenderTargetIdentifier identifier) // 接收 Unity 实际渲染目标标识。
        {
            Name = name; // 把传入的逻辑名称保存到 Name 属性里。

            Identifier = identifier; // 把传入的 Unity 渲染目标标识保存到 Identifier 属性里。

            IsValid = true; // 标记这个句柄是有效句柄，可以被 Pass 使用。
        }

        private BurtRenderTargetHandle(string name) // 定义私有构造函数，用来创建无效句柄。
        {
            Name = name; // 保存无效句柄的逻辑名称，方便调试时知道缺的是哪个目标。

            Identifier = default; // 使用默认 RenderTargetIdentifier 作为占位，避免无效句柄持有真实目标。

            IsValid = false; // 标记这个句柄无效，Pass 应该跳过使用它。
        }

        public static BurtRenderTargetHandle Invalid(string name) // 定义创建无效句柄的静态函数。
        {
            return new BurtRenderTargetHandle(name); // 返回一个带名称的无效句柄。
        }
    }

    public readonly struct BurtRenderBufferHandle // Logical buffer handle for RenderGraph validation and GPU buffer lookup.
    {
        public string Name { get; } // Buffer resource name used by builder declarations and debug output.

        public GraphicsBuffer Buffer { get; } // Actual GPU buffer owned or imported by the RenderGraph registry.

        public bool IsValid { get; } // Tracks whether the logical buffer exists in the current graph registry.

        public BurtRenderBufferHandle(string name)
            : this(name, null)
        {
        }

        public BurtRenderBufferHandle(string name, GraphicsBuffer buffer)
        {
            Name = name;
            Buffer = buffer;
            IsValid = true;
        }

        private BurtRenderBufferHandle(string name, bool isValid)
        {
            Name = name;
            Buffer = null;
            IsValid = isValid;
        }

        public bool HasBuffer => Buffer != null; // True after AllocateBuffer or when imported from outside the graph.

        public static BurtRenderBufferHandle Invalid(string name)
        {
            return new BurtRenderBufferHandle(name, false);
        }
    }

    public readonly struct BurtRenderBufferDescriptor // Describes a GPU buffer allocation owned by the RenderGraph.
    {
        public int Count { get; } // Element count.

        public int Stride { get; } // Element stride in bytes.

        public GraphicsBuffer.Target Target { get; } // Unity buffer target, usually Structured for tiled/cluster data.

        public string DebugName { get; } // Human-readable name assigned to GraphicsBuffer.name when allocated.

        public bool IsValid => Count > 0 && Stride > 0; // Minimal allocation guard.

        public BurtRenderBufferDescriptor(
            int count,
            int stride,
            GraphicsBuffer.Target target = GraphicsBuffer.Target.Structured,
            string debugName = null)
        {
            Count = count;
            Stride = stride;
            Target = target;
            DebugName = debugName;
        }
    }
}
