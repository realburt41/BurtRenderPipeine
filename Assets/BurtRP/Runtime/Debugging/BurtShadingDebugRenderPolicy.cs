namespace Burt.RenderPipeline
{
    internal enum BurtShadingDebugOutputStage
    {
        FullPipeline,
        DeferredGeometry
    }

    /// <summary>
    /// One camera-independent description of what the selected shading-debug mode needs.
    /// Forward and Deferred assemblers consume the same policy instead of duplicating
    /// post-process and scene-effect decisions.
    /// </summary>
    internal readonly struct BurtShadingDebugRenderPolicy
    {
        public readonly BurtShadingDebugMode Mode;
        public readonly bool IsEnabled;
        public readonly bool NeedsSceneEffects;
        public readonly bool NeedsTransparent;
        public readonly bool NeedsPostProcess;
        public readonly BurtShadingDebugOutputStage OutputStage;

        public bool PreserveOutputBeforeSceneEffects => IsEnabled && !NeedsSceneEffects;

        public bool CanTerminateAfterDeferredGeometry =>
            IsEnabled && OutputStage == BurtShadingDebugOutputStage.DeferredGeometry;

        public bool NeedsDeferredLightCulling =>
            !CanTerminateAfterDeferredGeometry ||
            Mode == BurtShadingDebugMode.TileLightCount ||
            Mode == BurtShadingDebugMode.TileLightOccupancy ||
            Mode == BurtShadingDebugMode.ClusterLightCount ||
            Mode == BurtShadingDebugMode.ClusterLightOccupancy;

        public bool NeedsMainLightShadowMap =>
            !CanTerminateAfterDeferredGeometry || Mode == BurtShadingDebugMode.MainLightShadow;

        public bool NeedsPerObjectShadowAtlas =>
            !CanTerminateAfterDeferredGeometry || Mode == BurtShadingDebugMode.PerObjectShadowAtlas;

        public bool NeedsAdditionalLightShadowAtlas => !CanTerminateAfterDeferredGeometry;

        private BurtShadingDebugRenderPolicy(
            BurtShadingDebugMode mode,
            bool isEnabled,
            bool needsSceneEffects,
            bool needsTransparent,
            bool needsPostProcess,
            BurtShadingDebugOutputStage outputStage)
        {
            Mode = mode;
            IsEnabled = isEnabled;
            NeedsSceneEffects = needsSceneEffects;
            NeedsTransparent = needsTransparent;
            NeedsPostProcess = needsPostProcess;
            OutputStage = outputStage;
        }

        public static BurtShadingDebugRenderPolicy Resolve(BurtRenderRequest request = null)
        {
            var mode = BurtShadingDebugSettings.IsShadingDebugAllowedForRequest(request)
                ? BurtShadingDebugSettings.Mode
                : BurtShadingDebugMode.None;
            if (mode == BurtShadingDebugMode.None)
            {
                return new BurtShadingDebugRenderPolicy(
                    mode,
                    false,
                    true,
                    true,
                    true,
                    BurtShadingDebugOutputStage.FullPipeline);
            }

            var isSceneEffect = BurtShadingDebugSettings.IsSceneEffectDebugMode(mode);
            var isPostProcessDebug = PostProcessUtility.IsBloomDebugRequested() ||
                PostProcessUtility.IsTemporalAADebugRequested() ||
                PostProcessUtility.IsAutoExposureDebugRequested();
            var canTerminateAfterGeometry = IsDeferredGeometryTerminalMode(mode);

            return new BurtShadingDebugRenderPolicy(
                mode,
                true,
                isSceneEffect || isPostProcessDebug,
                isSceneEffect || isPostProcessDebug,
                isSceneEffect || isPostProcessDebug,
                canTerminateAfterGeometry
                    ? BurtShadingDebugOutputStage.DeferredGeometry
                    : BurtShadingDebugOutputStage.FullPipeline);
        }

        private static bool IsDeferredGeometryTerminalMode(BurtShadingDebugMode mode)
        {
            if (mode == BurtShadingDebugMode.CameraDepth ||
                mode == BurtShadingDebugMode.MainLightShadow ||
                mode == BurtShadingDebugMode.PerObjectShadowAtlas ||
                mode == BurtShadingDebugMode.TileLightCount ||
                mode == BurtShadingDebugMode.TileLightOccupancy ||
                mode == BurtShadingDebugMode.ClusterLightCount ||
                mode == BurtShadingDebugMode.ClusterLightOccupancy)
            {
                return true;
            }

            return BurtGBufferDebugViewUtility.ResolveGBufferDebugViewMode(mode) !=
                BurtGBufferDebugViewMode.Disabled;
        }
    }
}
