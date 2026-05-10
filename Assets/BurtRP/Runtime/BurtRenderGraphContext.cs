using UnityEngine.Rendering; // 引入 Unity 渲染命名空间，用来使用 ScriptableRenderContext。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让这个上下文类和其他 BurtRP 代码处在同一个模块里。
{
    public sealed class BurtRenderGraphContext // 定义 RenderGraph 执行上下文，用来打包一次图执行需要的公共数据和资源表。
    {
        public ScriptableRenderContext ScriptableContext { get; } // 保存 Unity SRP 的渲染上下文，Pass 通过它提交绘制命令。

        public BurtRenderRequest Request { get; } // 保存当前正在执行的渲染请求，Pass 通过它读取 Camera、CullingResults 等任务数据。

        public BurtRenderPipelineAsset Asset { get; } // 保存当前管线资产，Pass 通过它读取默认清屏色等全局配置。

        public BurtRenderGraphResourceRegistry ResourceRegistry { get; } // 保存当前 RenderGraph 的资源注册表，Pass 通过它读取图资源。

        public BurtRenderTargetHandle CameraColorTarget // 定义读取 CameraColor 的快捷属性，方便 Pass 不直接操作资源名。
        {
            get // 定义属性 getter，每次访问时从资源注册表读取最新的 CameraColor。
            {
                if (ResourceRegistry == null) // 如果资源注册表为空，说明当前上下文没有可用资源表。
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraColorName); // 返回无效 CameraColor 句柄，避免 Pass 绑定错误目标。
                }

                return ResourceRegistry.GetCameraColor(); // 从资源注册表读取 CameraColor 句柄。
            }
        }

        public BurtRenderTargetHandle FinalCameraTarget // 定义读取最终相机输出目标的快捷属性，方便 FinalBlit 不直接操作资源名。
        {
            get // 定义属性 getter，每次访问时从资源注册表读取最新的 FinalCameraTarget。
            {
                if (ResourceRegistry == null) // 如果资源注册表为空，说明当前上下文没有可用资源表。
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FinalCameraTargetName); // 返回无效最终目标句柄，避免 FinalBlit 绑定错误输出。
                }

                return ResourceRegistry.GetFinalCameraTarget(); // 从资源注册表读取最终相机输出目标句柄。
            }
        }

        public BurtRenderTargetHandle CameraDepthTarget // 定义读取 CameraDepth 的快捷属性，方便 Pass 不直接操作资源名。
        {
            get // 定义属性 getter，每次访问时从资源注册表读取最新的 CameraDepth。
            {
                if (ResourceRegistry == null) // 如果资源注册表为空，说明当前上下文没有可用资源表。
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName); // 返回无效 CameraDepth 句柄，避免 Pass 绑定错误目标。
                }

                return ResourceRegistry.GetCameraDepth(); // 从资源注册表读取 CameraDepth 句柄。
            }
        }

        public BurtRenderTargetHandle PostProcessColorTarget // 定义读取 PostProcessColor 的快捷属性，方便后处理 Pass 不直接操作资源名。
        {
            get // 定义属性 getter，每次访问时从资源注册表读取最新的 PostProcessColor。
            {
                if (ResourceRegistry == null) // 如果资源注册表为空，说明当前上下文没有可用资源表。
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.PostProcessColorName); // 返回无效 PostProcessColor 句柄，避免 Pass 绑定错误目标。
                }

                return ResourceRegistry.GetPostProcessColor(); // 从资源注册表读取 PostProcessColor 句柄。
            }
        }

        public BurtRenderTargetHandle MainLightShadowMapTarget // 定义读取 MainLightShadowMap 的快捷属性，方便 Pass 不直接操作资源名。
        {
            get // 定义属性 getter，每次访问时从资源注册表读取最新的 MainLightShadowMap。
            {
                if (ResourceRegistry == null) // 如果资源注册表为空，说明当前上下文没有可用资源表。
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.MainLightShadowMapName); // 返回无效 MainLightShadowMap 句柄，避免 Pass 绑定错误阴影目标。
                }

                return ResourceRegistry.GetMainLightShadowMap(); // 从资源注册表读取 MainLightShadowMap 句柄。
            }
        }

        public BurtRenderTargetHandle GBuffer0Target // 定义读取 GBuffer0 的快捷属性，方便 Deferred Pass 不直接操作资源名。
        {
            get // 定义属性 getter，每次访问时从资源注册表读取最新的 GBuffer0。
            {
                if (ResourceRegistry == null) // 如果资源注册表为空，说明当前上下文没有可用资源表。
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer0Name); // 返回无效 GBuffer0 句柄，避免 Deferred Pass 绑定错误目标。
                }

                return ResourceRegistry.GetGBuffer0(); // 从资源注册表读取 GBuffer0 句柄。
            }
        }

        public BurtRenderTargetHandle GBuffer1Target // 定义读取 GBuffer1 的快捷属性，方便 Deferred Pass 不直接操作资源名。
        {
            get // 定义属性 getter，每次访问时从资源注册表读取最新的 GBuffer1。
            {
                if (ResourceRegistry == null) // 如果资源注册表为空，说明当前上下文没有可用资源表。
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer1Name); // 返回无效 GBuffer1 句柄，避免 Deferred Pass 绑定错误目标。
                }

                return ResourceRegistry.GetGBuffer1(); // 从资源注册表读取 GBuffer1 句柄。
            }
        }

        public BurtRenderTargetHandle GBuffer2Target // 定义读取 GBuffer2 的快捷属性，方便 Deferred Pass 不直接操作资源名。
        {
            get // 定义属性 getter，每次访问时从资源注册表读取最新的 GBuffer2。
            {
                if (ResourceRegistry == null) // 如果资源注册表为空，说明当前上下文没有可用资源表。
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer2Name); // 返回无效 GBuffer2 句柄，避免 Deferred Pass 绑定错误目标。
                }

                return ResourceRegistry.GetGBuffer2(); // 从资源注册表读取 GBuffer2 句柄。
            }
        }

        public BurtRenderTargetHandle HiZDepthTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.HiZDepthName);
                }

                return ResourceRegistry.GetHiZDepth();
            }
        }

        public BurtRenderTargetHandle ScreenSpaceReflectionColorTarget
        {
            get
            {
                if (ResourceRegistry == null)
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceReflectionColorName);
                }

                return ResourceRegistry.GetScreenSpaceReflectionColor();
            }
        }

        public BurtRequestRenderOptions RenderOptions { get; } // 保存当前 request 的栈级执行选项，Pass 可以通过它判断 RT 生命周期策略。

        public BurtRenderGraphContext( // 保留旧构造函数，让没有显式传入执行选项的调用方继续走单 request 生命周期。
            ScriptableRenderContext scriptableContext, // 接收 Unity SRP 传入的渲染上下文。
            BurtRenderRequest request, // 接收当前正在执行的 Burt 渲染请求。
            BurtRenderPipelineAsset asset, // 接收 BurtRP 管线资产配置。
            BurtRenderGraphResourceRegistry resourceRegistry) // 接收当前 RenderGraph 的资源注册表。
            : this(scriptableContext, request, asset, resourceRegistry, BurtRequestRenderOptions.CreateSingleRequest()) // 把旧调用统一转发到新构造函数，并使用旧行为默认选项。
        {
        }

        public BurtRenderGraphContext( // 定义新构造函数，用来创建一次带栈级执行选项的 RenderGraph 执行上下文。
            ScriptableRenderContext scriptableContext, // 接收 Unity SRP 传入的渲染上下文。
            BurtRenderRequest request, // 接收当前正在执行的 Burt 渲染请求。
            BurtRenderPipelineAsset asset, // 接收 BurtRP 管线资产配置。
            BurtRenderGraphResourceRegistry resourceRegistry, // 接收当前 RenderGraph 的资源注册表。
            BurtRequestRenderOptions renderOptions) // 接收当前 request 的栈级 RenderTarget 生命周期选项。
        {
            ScriptableContext = scriptableContext; // 把 Unity SRP 渲染上下文保存到 ScriptableContext 属性里。

            Request = request; // 把当前渲染请求保存到 Request 属性里。

            Asset = asset; // 把管线资产保存到 Asset 属性里。

            ResourceRegistry = resourceRegistry; // 把 RenderGraph 的资源注册表保存到 ResourceRegistry 属性里。

            RenderOptions = renderOptions ?? BurtRequestRenderOptions.CreateSingleRequest(); // 保存执行选项，传入空值时回退到旧单 request 生命周期。
        }
    }
}
