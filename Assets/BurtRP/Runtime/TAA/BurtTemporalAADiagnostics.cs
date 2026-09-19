#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    // Opt-in editor instrumentation. Capture must be recorded before temporary
    // TAA resources are released/reused; endCameraRendering is too late.
    public static class BurtTemporalAADiagnostics
    {
        // Test only: keep compute depth/MV preparation while selecting the
        // native fragment resolve. Defaults off and is restored by the fixture.
        public static bool ForceNativeRasterResolve;
        // Editor A/B only. Production native TAA retains bounded motion refresh.
        public static bool DisableNativeMotionRefresh;
        // Opt-in lab candidate replay after native resolve but before history
        // storage. No subscriber means no extra commands or allocations.
        public delegate void NativeResolveExperimentCallback(CommandBuffer cmd, Camera camera,
            BurtTemporalAARequestState state, RenderTargetIdentifier source,
            RenderTargetIdentifier history, RenderTargetIdentifier velocity,
            RenderTargetIdentifier validity, RenderTargetIdentifier stencil,
            RenderTargetIdentifier resolved, RenderTextureDescriptor descriptor);
        public static NativeResolveExperimentCallback NativeResolveExperiment;
        public static Action<CommandBuffer, Camera, BurtTemporalAARequestState, string,
            RenderTargetIdentifier, RenderTextureDescriptor> Capture;
    }
}
#endif
