using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    public enum ColorGradingTemperatureMode
    {
        WhiteBalance = 0,
        ColorTemperature = 1
    }

    [Serializable]
    [VolumeComponentMenu("Post Processing/Color Grading")]
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtColorGradingVolumeComponent")]
    public sealed class ColorGradingVolumeComponent : VolumeComponent
    {
        private const float NeutralEpsilon = 0.0001f;

        [Title("White Balance")]
        public BoolParameter enableWhiteBalance = new BoolParameter(false);
        public ColorGradingTemperatureModeParameter temperatureMode = new ColorGradingTemperatureModeParameter(ColorGradingTemperatureMode.WhiteBalance);
        public ClampedFloatParameter whiteTemp = new ClampedFloatParameter(ColorGradingSettings.DefaultWhiteTemp, 1500f, 15000f);
        public ClampedFloatParameter whiteTint = new ClampedFloatParameter(ColorGradingSettings.DefaultWhiteTint, -1f, 1f);

        [Title("Color Grading")]
        public BoolParameter enableColorGrading = new BoolParameter(false);
        public Vector4Parameter globalSaturation = new Vector4Parameter(ColorGradingSettings.DefaultColorVector);
        public Vector4Parameter globalContrast = new Vector4Parameter(ColorGradingSettings.DefaultColorVector);
        public Vector4Parameter globalGamma = new Vector4Parameter(ColorGradingSettings.DefaultColorVector);
        public Vector4Parameter globalGain = new Vector4Parameter(ColorGradingSettings.DefaultColorVector);
        public Vector4Parameter globalOffset = new Vector4Parameter(ColorGradingSettings.DefaultOffsetVector);

        [Title("Shadows")]
        public Vector4Parameter shadowsSaturation = new Vector4Parameter(ColorGradingSettings.DefaultColorVector);
        public Vector4Parameter shadowsContrast = new Vector4Parameter(ColorGradingSettings.DefaultColorVector);
        public Vector4Parameter shadowsGamma = new Vector4Parameter(ColorGradingSettings.DefaultColorVector);
        public Vector4Parameter shadowsGain = new Vector4Parameter(ColorGradingSettings.DefaultColorVector);
        public Vector4Parameter shadowsOffset = new Vector4Parameter(ColorGradingSettings.DefaultOffsetVector);
        public ClampedFloatParameter shadowsMax = new ClampedFloatParameter(ColorGradingSettings.DefaultShadowsMax, -1f, 1f);

        [Title("Midtones")]
        public Vector4Parameter midtonesSaturation = new Vector4Parameter(ColorGradingSettings.DefaultColorVector);
        public Vector4Parameter midtonesContrast = new Vector4Parameter(ColorGradingSettings.DefaultColorVector);
        public Vector4Parameter midtonesGamma = new Vector4Parameter(ColorGradingSettings.DefaultColorVector);
        public Vector4Parameter midtonesGain = new Vector4Parameter(ColorGradingSettings.DefaultColorVector);
        public Vector4Parameter midtonesOffset = new Vector4Parameter(ColorGradingSettings.DefaultOffsetVector);

        [Title("Highlights")]
        public Vector4Parameter highlightsSaturation = new Vector4Parameter(ColorGradingSettings.DefaultColorVector);
        public Vector4Parameter highlightsContrast = new Vector4Parameter(ColorGradingSettings.DefaultColorVector);
        public Vector4Parameter highlightsGamma = new Vector4Parameter(ColorGradingSettings.DefaultColorVector);
        public Vector4Parameter highlightsGain = new Vector4Parameter(ColorGradingSettings.DefaultColorVector);
        public Vector4Parameter highlightsOffset = new Vector4Parameter(ColorGradingSettings.DefaultOffsetVector);
        public ClampedFloatParameter highlightsMin = new ClampedFloatParameter(ColorGradingSettings.DefaultHighlightsMin, -1f, 1f);
        public ClampedFloatParameter highlightsMax = new ClampedFloatParameter(ColorGradingSettings.DefaultHighlightsMax, 1f, 10f);

        [Title("LUT")]
        public TextureParameter colorGradingLUT = new TextureParameter(null);
        public ClampedFloatParameter colorGradingIntensity = new ClampedFloatParameter(ColorGradingSettings.DefaultIntensity, 0f, 1f);
        public ClampedFloatParameter colorLUTContribution = new ClampedFloatParameter(ColorGradingSettings.DefaultLutContribution, 0f, 1f);
        public ClampedIntParameter colorLUTSize = new ClampedIntParameter(ColorGradingSettings.DefaultLutSize, 2, 64);

        public bool IsEnabled()
        {
            if (!active)
            {
                return false;
            }

            if (enableWhiteBalance.value)
            {
                return true;
            }

            if (enableColorGrading.value && HasAnyColorGradingChange())
            {
                return true;
            }

            return colorGradingLUT.value != null && colorLUTContribution.value > NeutralEpsilon;
        }

        private bool HasAnyColorGradingChange()
        {
            return !IsNeutral(globalSaturation.value, ColorGradingSettings.DefaultColorVector)
                || !IsNeutral(globalContrast.value, ColorGradingSettings.DefaultColorVector)
                || !IsNeutral(globalGamma.value, ColorGradingSettings.DefaultColorVector)
                || !IsNeutral(globalGain.value, ColorGradingSettings.DefaultColorVector)
                || !IsNeutral(globalOffset.value, ColorGradingSettings.DefaultOffsetVector)
                || !IsNeutral(shadowsSaturation.value, ColorGradingSettings.DefaultColorVector)
                || !IsNeutral(shadowsContrast.value, ColorGradingSettings.DefaultColorVector)
                || !IsNeutral(shadowsGamma.value, ColorGradingSettings.DefaultColorVector)
                || !IsNeutral(shadowsGain.value, ColorGradingSettings.DefaultColorVector)
                || !IsNeutral(shadowsOffset.value, ColorGradingSettings.DefaultOffsetVector)
                || !IsNeutral(midtonesSaturation.value, ColorGradingSettings.DefaultColorVector)
                || !IsNeutral(midtonesContrast.value, ColorGradingSettings.DefaultColorVector)
                || !IsNeutral(midtonesGamma.value, ColorGradingSettings.DefaultColorVector)
                || !IsNeutral(midtonesGain.value, ColorGradingSettings.DefaultColorVector)
                || !IsNeutral(midtonesOffset.value, ColorGradingSettings.DefaultOffsetVector)
                || !IsNeutral(highlightsSaturation.value, ColorGradingSettings.DefaultColorVector)
                || !IsNeutral(highlightsContrast.value, ColorGradingSettings.DefaultColorVector)
                || !IsNeutral(highlightsGamma.value, ColorGradingSettings.DefaultColorVector)
                || !IsNeutral(highlightsGain.value, ColorGradingSettings.DefaultColorVector)
                || !IsNeutral(highlightsOffset.value, ColorGradingSettings.DefaultOffsetVector)
                || Mathf.Abs(shadowsMax.value - ColorGradingSettings.DefaultShadowsMax) > NeutralEpsilon
                || Mathf.Abs(highlightsMin.value - ColorGradingSettings.DefaultHighlightsMin) > NeutralEpsilon
                || Mathf.Abs(highlightsMax.value - ColorGradingSettings.DefaultHighlightsMax) > NeutralEpsilon
                || colorGradingIntensity.value < 1f - NeutralEpsilon;
        }

        private static bool IsNeutral(Vector4 value, Vector4 neutral)
        {
            return Mathf.Abs(value.x - neutral.x) <= NeutralEpsilon
                && Mathf.Abs(value.y - neutral.y) <= NeutralEpsilon
                && Mathf.Abs(value.z - neutral.z) <= NeutralEpsilon
                && Mathf.Abs(value.w - neutral.w) <= NeutralEpsilon;
        }
    }
}
