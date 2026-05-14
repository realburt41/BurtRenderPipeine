using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    [Serializable]
    [VolumeComponentMenu("BurtRP/Post Processing/Color Adjustments")]
    public sealed class BurtColorAdjustmentsVolumeComponent : VolumeComponent
    {
        private const float NeutralEpsilon = 0.0001f;

        [Title("BurtRP Color Adjustments")]
        [InfoBox("Runs inside the Burt post-process pass after tonemapping.")]
        public ClampedFloatParameter saturation = new ClampedFloatParameter(BurtColorAdjustmentsSettings.DefaultSaturation, 0f, 2f);
        public ClampedFloatParameter contrast = new ClampedFloatParameter(BurtColorAdjustmentsSettings.DefaultContrast, 0f, 2f);
        public ClampedFloatParameter gamma = new ClampedFloatParameter(BurtColorAdjustmentsSettings.DefaultGamma, 0.01f, 5f);
        public ColorParameter colorFilter = new ColorParameter(BurtColorAdjustmentsSettings.DefaultColorFilter, false, false, true);

        public bool IsEnabled()
        {
            return active && (HasAnyOverride() || HasAnyNonNeutralValue());
        }

        private bool HasAnyOverride()
        {
            return saturation.overrideState || contrast.overrideState || gamma.overrideState || colorFilter.overrideState;
        }

        private bool HasAnyNonNeutralValue()
        {
            if (Mathf.Abs(saturation.value - BurtColorAdjustmentsSettings.DefaultSaturation) > NeutralEpsilon)
            {
                return true;
            }

            if (Mathf.Abs(contrast.value - BurtColorAdjustmentsSettings.DefaultContrast) > NeutralEpsilon)
            {
                return true;
            }

            if (Mathf.Abs(gamma.value - BurtColorAdjustmentsSettings.DefaultGamma) > NeutralEpsilon)
            {
                return true;
            }

            var filter = colorFilter.value;
            return Mathf.Abs(filter.r - BurtColorAdjustmentsSettings.DefaultColorFilter.r) > NeutralEpsilon
                || Mathf.Abs(filter.g - BurtColorAdjustmentsSettings.DefaultColorFilter.g) > NeutralEpsilon
                || Mathf.Abs(filter.b - BurtColorAdjustmentsSettings.DefaultColorFilter.b) > NeutralEpsilon;
        }
    }
}
