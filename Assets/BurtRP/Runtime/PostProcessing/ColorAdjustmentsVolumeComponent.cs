using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    [Serializable]
    [VolumeComponentMenu("Post Processing/Color Adjustments")]
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtColorAdjustmentsVolumeComponent")]
    public sealed class ColorAdjustmentsVolumeComponent : VolumeComponent
    {
        private const float NeutralEpsilon = 0.0001f;

        [Title("BurtRP Color Adjustments")]
        [InfoBox("Runs inside the Burt post-process pass after tonemapping.")]
        public BoolParameter enabled = new BoolParameter(true);
        public ClampedFloatParameter saturation = new ClampedFloatParameter(ColorAdjustmentsSettings.DefaultSaturation, 0f, 2f);
        public ClampedFloatParameter contrast = new ClampedFloatParameter(ColorAdjustmentsSettings.DefaultContrast, 0f, 2f);
        public ClampedFloatParameter gamma = new ClampedFloatParameter(ColorAdjustmentsSettings.DefaultGamma, 0.01f, 5f);
        public ColorParameter colorFilter = new ColorParameter(ColorAdjustmentsSettings.DefaultColorFilter, false, false, true);

        public bool IsEnabled()
        {
            return active && enabled.value && (HasAnyOverride() || HasAnyNonNeutralValue());
        }

        private bool HasAnyOverride()
        {
            return saturation.overrideState || contrast.overrideState || gamma.overrideState || colorFilter.overrideState;
        }

        private bool HasAnyNonNeutralValue()
        {
            if (Mathf.Abs(saturation.value - ColorAdjustmentsSettings.DefaultSaturation) > NeutralEpsilon)
            {
                return true;
            }

            if (Mathf.Abs(contrast.value - ColorAdjustmentsSettings.DefaultContrast) > NeutralEpsilon)
            {
                return true;
            }

            if (Mathf.Abs(gamma.value - ColorAdjustmentsSettings.DefaultGamma) > NeutralEpsilon)
            {
                return true;
            }

            var filter = colorFilter.value;
            return Mathf.Abs(filter.r - ColorAdjustmentsSettings.DefaultColorFilter.r) > NeutralEpsilon
                || Mathf.Abs(filter.g - ColorAdjustmentsSettings.DefaultColorFilter.g) > NeutralEpsilon
                || Mathf.Abs(filter.b - ColorAdjustmentsSettings.DefaultColorFilter.b) > NeutralEpsilon;
        }
    }
}
