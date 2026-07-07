using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    [Serializable]
    [VolumeComponentMenu("Post Processing/FXAA")]
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtFastApproximateAAVolumeComponent")]
    public sealed class FastApproximateAAVolumeComponent : VolumeComponent
    {
        [Title("FXAA")]
        public ClampedFloatParameter subpixel = new ClampedFloatParameter(FastApproximateAASettings.DefaultSubpixel, 0f, 1f);
        public ClampedFloatParameter edgeThreshold = new ClampedFloatParameter(FastApproximateAASettings.DefaultEdgeThreshold, 0.0312f, 0.333f);
        public ClampedFloatParameter edgeThresholdMin = new ClampedFloatParameter(FastApproximateAASettings.DefaultEdgeThresholdMin, 0f, 0.0833f);

        public bool IsEnabled()
        {
            return active;
        }
    }
}
