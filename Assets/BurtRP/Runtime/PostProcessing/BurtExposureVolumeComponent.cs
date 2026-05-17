using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    [Serializable]
    [VolumeComponentMenu("BurtRP/Post Processing/Exposure")]
    public sealed class BurtExposureVolumeComponent : VolumeComponent
    {
        [Title("BurtRP Exposure")]
        [InfoBox("Exposure is applied before tonemapping. Final multiplier = 2^(-EV100 + Compensation) * Calibration.")]
        public BurtExposureModeParameter mode = new BurtExposureModeParameter(BurtExposureMode.ManualEV100);
        public ClampedFloatParameter manualEV100 = new ClampedFloatParameter(BurtPhysicalExposureSettings.DefaultManualEv100, -16f, 24f);
        public ClampedFloatParameter compensation = new ClampedFloatParameter(BurtPhysicalExposureSettings.DefaultCompensation, -16f, 16f);
        public ClampedFloatParameter calibration = new ClampedFloatParameter(BurtPhysicalExposureSettings.DefaultCalibration, 0f, 1024f);

        [Title("Physical Camera")]
        public ClampedFloatParameter iso = new ClampedFloatParameter(BurtPhysicalExposureSettings.DefaultIso, 1f, 204800f);
        public ClampedFloatParameter shutterTime = new ClampedFloatParameter(BurtPhysicalExposureSettings.DefaultShutterTime, 0.000001f, 60f);
        public ClampedFloatParameter aperture = new ClampedFloatParameter(BurtPhysicalExposureSettings.DefaultAperture, 0.1f, 64f);

        [Title("Automatic")]
        public ClampedFloatParameter autoMinEV100 = new ClampedFloatParameter(BurtPhysicalExposureSettings.DefaultAutoMinEv100, -16f, 24f);
        public ClampedFloatParameter autoMaxEV100 = new ClampedFloatParameter(BurtPhysicalExposureSettings.DefaultAutoMaxEv100, -16f, 24f);
        public ClampedFloatParameter autoMiddleGrey = new ClampedFloatParameter(BurtPhysicalExposureSettings.DefaultAutoMiddleGrey, 0.001f, 1f);
        public ClampedFloatParameter autoSpeedUp = new ClampedFloatParameter(BurtPhysicalExposureSettings.DefaultAutoSpeedUp, 0.02f, 20f);
        public ClampedFloatParameter autoSpeedDown = new ClampedFloatParameter(BurtPhysicalExposureSettings.DefaultAutoSpeedDown, 0.02f, 20f);
        public ClampedFloatParameter autoLowPercent = new ClampedFloatParameter(BurtPhysicalExposureSettings.DefaultAutoLowPercent, 0f, 100f);
        public ClampedFloatParameter autoHighPercent = new ClampedFloatParameter(BurtPhysicalExposureSettings.DefaultAutoHighPercent, 0f, 100f);
        public ClampedFloatParameter autoHistogramMinEV100 = new ClampedFloatParameter(BurtPhysicalExposureSettings.DefaultAutoHistogramMinEv100, -16f, 24f);
        public ClampedFloatParameter autoHistogramMaxEV100 = new ClampedFloatParameter(BurtPhysicalExposureSettings.DefaultAutoHistogramMaxEv100, -16f, 24f);

        public bool IsEnabled()
        {
            if (!active)
            {
                return false;
            }

            if (mode.value == BurtExposureMode.Automatic || mode.value == BurtExposureMode.AutomaticHistogram)
            {
                return true;
            }

            return mode.value != BurtPhysicalExposureSettings.DefaultMode ||
                Mathf.Abs(manualEV100.value - BurtPhysicalExposureSettings.DefaultManualEv100) > 0.0001f ||
                Mathf.Abs(compensation.value - BurtPhysicalExposureSettings.DefaultCompensation) > 0.0001f ||
                Mathf.Abs(calibration.value - BurtPhysicalExposureSettings.DefaultCalibration) > 0.0001f ||
                Mathf.Abs(iso.value - BurtPhysicalExposureSettings.DefaultIso) > 0.0001f ||
                Mathf.Abs(shutterTime.value - BurtPhysicalExposureSettings.DefaultShutterTime) > 0.000001f ||
                Mathf.Abs(aperture.value - BurtPhysicalExposureSettings.DefaultAperture) > 0.0001f;
        }
    }
}
