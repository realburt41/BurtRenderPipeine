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

        public bool IsEnabled()
        {
            if (!active)
            {
                return false;
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
