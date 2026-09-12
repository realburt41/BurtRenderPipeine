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
        public static Action<CommandBuffer, Camera, BurtTemporalAARequestState, string,
            RenderTargetIdentifier, RenderTextureDescriptor> Capture;
    }
}
#endif
