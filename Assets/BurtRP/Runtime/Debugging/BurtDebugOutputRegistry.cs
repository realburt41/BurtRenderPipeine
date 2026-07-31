using System.Collections.Generic;

namespace Burt.RenderPipeline
{
    /// <summary>
    /// Collects debug presenters from pipeline modules and emits them at one defined
    /// presentation point. The list is reused to avoid per-camera allocations.
    /// </summary>
    internal sealed class BurtDebugOutputRegistry
    {
        private readonly List<BurtRenderPass> outputs = new List<BurtRenderPass>(8);

        public void Clear()
        {
            outputs.Clear();
        }

        public void Register(BurtRenderPass output)
        {
            if (output != null && !outputs.Contains(output))
            {
                outputs.Add(output);
            }
        }

        public void Emit(BurtRenderGraph graph)
        {
            if (graph == null)
            {
                outputs.Clear();
                return;
            }

            for (var index = 0; index < outputs.Count; index++)
            {
                graph.AddPass(outputs[index]);
            }

            outputs.Clear();
        }
    }
}
