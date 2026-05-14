using System;
using Sirenix.OdinInspector;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    [Serializable]
    [VolumeComponentMenu("BurtRP/Post Processing/Temporal AA")]
    public sealed class BurtTemporalAAVolumeComponent : VolumeComponent
    {
        [Title("BurtRP Temporal AA")]
        [InfoBox("Halton jitter + depth/color/confidence history with tunable rejection and feedback.")]
        public BoolParameter enabled = new BoolParameter(false);
        public ClampedFloatParameter feedback = new ClampedFloatParameter(0.93f, 0f, 0.99f);
        public ClampedFloatParameter jitterScale = new ClampedFloatParameter(1.0f, 0f, 2f);
        public ClampedFloatParameter clampStrength = new ClampedFloatParameter(0.95f, 0.25f, 4f);
        public ClampedFloatParameter sharpness = new ClampedFloatParameter(0.04f, 0f, 0.3f);
        public ClampedFloatParameter staticEdgeRelaxation = new ClampedFloatParameter(0.28f, 0f, 1f);
        public ClampedFloatParameter lumaRejectionStrength = new ClampedFloatParameter(1.15f, 0f, 4f);
        public ClampedFloatParameter clipRejectionStrength = new ClampedFloatParameter(1.25f, 0f, 4f);
        public ClampedFloatParameter depthRejectionStrength = new ClampedFloatParameter(1.35f, 0f, 4f);
        public ClampedFloatParameter motionRejectionStart = new ClampedFloatParameter(16f, 0f, 128f);
        public ClampedFloatParameter motionRejectionRange = new ClampedFloatParameter(56f, 1f, 256f);
        public ClampedFloatParameter historyConfidenceWeight = new ClampedFloatParameter(0.22f, 0f, 1f);
        public ClampedFloatParameter historyConfidenceBoost = new ClampedFloatParameter(0.96f, 0.5f, 1.1f);
        public ClampedFloatParameter confidenceGrowth = new ClampedFloatParameter(0.06f, 0f, 0.25f);

        [Title("XRender TSR")]
        public ClampedFloatParameter antiFlickering = new ClampedFloatParameter(0.5f, 0f, 1f);
        public ClampedFloatParameter motionVectorRejection = new ClampedFloatParameter(0f, 0f, 1f);
        public ClampedFloatParameter baseBlendFactor = new ClampedFloatParameter(0.875f, 0f, 0.99f);
        public ClampedFloatParameter responsiveRejectionStrength = new ClampedFloatParameter(0.65f, 0f, 1f);
        public ClampedFloatParameter untrustedMotionFeedbackScale = new ClampedFloatParameter(0.35f, 0f, 1f);
        public ClampedFloatParameter disocclusionFeedbackScale = new ClampedFloatParameter(0.18f, 0f, 1f);

        [Title("Edge Rejection")]
        public ClampedFloatParameter motionEdgeResponsiveStrength = new ClampedFloatParameter(1.2f, 0f, 3f);
        public ClampedFloatParameter depthEdgeResponsiveStrength = new ClampedFloatParameter(1.1f, 0f, 3f);
        public ClampedFloatParameter historyClampTightness = new ClampedFloatParameter(1.15f, 0f, 2f);
        public ClampedFloatParameter depthWeightedFilterFloor = new ClampedFloatParameter(0.12f, 0f, 0.5f);

        public bool IsEnabled()
        {
            return active && enabled.value;
        }
    }
}
