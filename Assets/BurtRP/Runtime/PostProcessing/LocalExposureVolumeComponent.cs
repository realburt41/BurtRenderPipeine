using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    [Serializable]
    [VolumeComponentMenu("Post Processing/Local Exposure")]
    public sealed class LocalExposureVolumeComponent : VolumeComponent
    {
        [Title("XRender Local Exposure")]
        [InfoBox("The effect is opt-in: it remains disabled until this override is added and Enabled is checked.")]
        public BoolParameter enabled = new BoolParameter(false);

        public ClampedFloatParameter highlightContrast = new ClampedFloatParameter(0.75f, 0f, 1f);
        public AnimationCurveParameter highlightContrastCurve = new AnimationCurveParameter(
            new AnimationCurve(new Keyframe(-10f, 1f), new Keyframe(20f, 1f)));
        public ClampedFloatParameter shadowContrast = new ClampedFloatParameter(0.75f, 0f, 1f);
        public AnimationCurveParameter shadowContrastCurve = new AnimationCurveParameter(
            new AnimationCurve(new Keyframe(-10f, 1f), new Keyframe(20f, 1f)));
        public ClampedFloatParameter highlightThreshold = new ClampedFloatParameter(0f, 0f, 1f);
        public ClampedFloatParameter shadowThreshold = new ClampedFloatParameter(0f, 0f, 1f);
        public ClampedFloatParameter detailStrength = new ClampedFloatParameter(1f, 0f, 4f);
        public ClampedFloatParameter blurredLuminanceBlend = new ClampedFloatParameter(0.6f, 0f, 1f);
        public ClampedFloatParameter blurredLuminanceKernelSizePercent = new ClampedFloatParameter(50f, 0f, 100f);
        public ClampedFloatParameter middleGreyBias = new ClampedFloatParameter(0f, -15f, 15f);

        public bool IsEnabled()
        {
            return active && enabled.value;
        }
    }
}
