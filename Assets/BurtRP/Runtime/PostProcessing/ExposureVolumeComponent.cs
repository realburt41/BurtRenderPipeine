using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    [Serializable]
    [VolumeComponentMenu("Post Processing/Exposure")]
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtExposureVolumeComponent")]
    public sealed class ExposureVolumeComponent : VolumeComponent
    {
        [Title("BurtRP Exposure")]
        [InfoBox("Exposure is applied before tonemapping. Final multiplier = 2^(-EV100 + Compensation) * Calibration.")]
        public BoolParameter enabled = new BoolParameter(true);
        public ExposureModeParameter mode = new ExposureModeParameter(ExposureMode.ManualEV100);
        public ClampedFloatParameter manualEV100 = new ClampedFloatParameter(PhysicalExposureSettings.DefaultManualEv100, -16f, 24f);
        public ClampedFloatParameter compensation = new ClampedFloatParameter(PhysicalExposureSettings.DefaultCompensation, -16f, 16f);
        public ClampedFloatParameter calibration = new ClampedFloatParameter(PhysicalExposureSettings.DefaultCalibration, 0f, 1024f);

        [Title("Physical Camera")]
        public ClampedFloatParameter iso = new ClampedFloatParameter(PhysicalExposureSettings.DefaultIso, 1f, 204800f);
        public ClampedFloatParameter shutterTime = new ClampedFloatParameter(PhysicalExposureSettings.DefaultShutterTime, 0.000001f, 60f);
        public ClampedFloatParameter aperture = new ClampedFloatParameter(PhysicalExposureSettings.DefaultAperture, 0.1f, 64f);

        [Title("Automatic")]
        public ClampedFloatParameter autoMinEV100 = new ClampedFloatParameter(PhysicalExposureSettings.DefaultAutoMinEv100, -16f, 24f);
        public ClampedFloatParameter autoMaxEV100 = new ClampedFloatParameter(PhysicalExposureSettings.DefaultAutoMaxEv100, -16f, 24f);
        public ClampedFloatParameter autoMiddleGrey = new ClampedFloatParameter(PhysicalExposureSettings.DefaultAutoMiddleGrey, 0.001f, 1f);
        public ClampedFloatParameter autoSpeedUp = new ClampedFloatParameter(PhysicalExposureSettings.DefaultAutoSpeedUp, 0.02f, 20f);
        public ClampedFloatParameter autoSpeedDown = new ClampedFloatParameter(PhysicalExposureSettings.DefaultAutoSpeedDown, 0.02f, 20f);
        public ClampedFloatParameter autoLowPercent = new ClampedFloatParameter(PhysicalExposureSettings.DefaultAutoLowPercent, 0f, 100f);
        public ClampedFloatParameter autoHighPercent = new ClampedFloatParameter(PhysicalExposureSettings.DefaultAutoHighPercent, 0f, 100f);
        public ClampedFloatParameter autoHistogramMinEV100 = new ClampedFloatParameter(PhysicalExposureSettings.DefaultAutoHistogramMinEv100, -16f, 24f);
        public ClampedFloatParameter autoHistogramMaxEV100 = new ClampedFloatParameter(PhysicalExposureSettings.DefaultAutoHistogramMaxEv100, -16f, 24f);

        public bool IsEnabled()
        {
            if (!active || !enabled.value)
            {
                return false;
            }

            if (mode.value == ExposureMode.Automatic || mode.value == ExposureMode.AutomaticHistogram)
            {
                return true;
            }

            return mode.value != PhysicalExposureSettings.DefaultMode ||
                Mathf.Abs(manualEV100.value - PhysicalExposureSettings.DefaultManualEv100) > 0.0001f ||
                Mathf.Abs(compensation.value - PhysicalExposureSettings.DefaultCompensation) > 0.0001f ||
                Mathf.Abs(calibration.value - PhysicalExposureSettings.DefaultCalibration) > 0.0001f ||
                Mathf.Abs(iso.value - PhysicalExposureSettings.DefaultIso) > 0.0001f ||
                Mathf.Abs(shutterTime.value - PhysicalExposureSettings.DefaultShutterTime) > 0.000001f ||
                Mathf.Abs(aperture.value - PhysicalExposureSettings.DefaultAperture) > 0.0001f;
        }
    }
}
