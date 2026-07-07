using System;
using Sirenix.OdinInspector;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    [Serializable]
    [VolumeComponentMenu("Post Processing/Temporal AA")]
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtTemporalAAVolumeComponent")]
    public sealed class TemporalAAVolumeComponent : VolumeComponent
    {
        [Title("BurtRP Temporal AA")]
        [InfoBox("XRender-style Halton jitter with color/depth history, parallax rejection, and object-motion responsive AA.")]
        public BoolParameter enabled = new BoolParameter(false);
        public ClampedFloatParameter jitterScale = new ClampedFloatParameter(1.0f, 0f, 2f);
        public ClampedFloatParameter sharpness = new ClampedFloatParameter(0.04f, 0f, 0.3f);

        [Title("XRender TSR")]
        public ClampedFloatParameter untrustedMotionFeedbackScale = new ClampedFloatParameter(0.35f, 0f, 1f);
        public ClampedFloatParameter upscaleFactor = new ClampedFloatParameter(1.0f, 1f, 2f);

        [Title("Edge Rejection")]
        public ClampedFloatParameter motionEdgeResponsiveStrength = new ClampedFloatParameter(1.2f, 0f, 3f);
        public ClampedFloatParameter depthEdgeResponsiveStrength = new ClampedFloatParameter(1.1f, 0f, 3f);

        [Title("Debug Temporal")]
        public BoolParameter debugFreezeJitter = new BoolParameter(false);
        public ClampedIntParameter debugJitterFrame = new ClampedIntParameter(0, 0, 1023);
        public BoolParameter debugOverrideJitterScale = new BoolParameter(false);
        public ClampedFloatParameter debugJitterScale = new ClampedFloatParameter(1.0f, 0f, 2f);

        public bool IsEnabled()
        {
            return active && enabled.value;
        }
    }
}
