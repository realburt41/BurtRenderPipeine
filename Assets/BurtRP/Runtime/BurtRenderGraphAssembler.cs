using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让这个类和其他 BurtRP 运行时代码处在同一个模块里。
{
    public abstract class BurtRenderGraphAssembler
    {
        public abstract string Name { get; } // 定义组装器名称，用于调试、Frame Debugger、Profiler 或日志显示。

        public abstract void Assemble( // 定义组装函数，具体子类负责把 Pass 添加进 BurtRenderGraph。
            BurtRenderGraph graph, // 接收要被填充的 BurtRenderGraph。
            BurtRenderRequest request, // 接收当前渲染请求，用来判断这次渲染任务是什么。
            BurtRenderPipelineAsset asset); // 接收管线资产配置，用来读取管线级开关和默认参数。
    }
}