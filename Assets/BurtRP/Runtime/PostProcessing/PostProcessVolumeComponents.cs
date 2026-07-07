using System;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtTonemappingModeParameter")]
    public sealed class TonemappingModeParameter : VolumeParameter<TonemappingMode>
    {
        public TonemappingModeParameter(TonemappingMode value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtBloomQualityParameter")]
    public sealed class BloomQualityParameter : VolumeParameter<BloomQuality>, IEquatable<BloomQualityParameter>
    {
        public BloomQualityParameter(BloomQuality value, bool overrideState = false)
            : base(value, overrideState)
        {
        }

        public static bool operator ==(BloomQualityParameter lhs, BloomQuality rhs)
        {
            return lhs != null && lhs.value == rhs;
        }

        public static bool operator !=(BloomQualityParameter lhs, BloomQuality rhs)
        {
            return !(lhs == rhs);
        }

        public override int GetHashCode()
        {
            return ((int)value * 31) + (overrideState ? 1 : 0);
        }

        public bool Equals(BloomQualityParameter other)
        {
            return other != null && value == other.value && overrideState == other.overrideState;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as BloomQualityParameter);
        }
    }

    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtBloomDebugViewParameter")]
    public sealed class BloomDebugViewParameter : VolumeParameter<BloomDebugView>, IEquatable<BloomDebugViewParameter>
    {
        public BloomDebugViewParameter(BloomDebugView value, bool overrideState = false)
            : base(value, overrideState)
        {
        }

        public static bool operator ==(BloomDebugViewParameter lhs, BloomDebugView rhs)
        {
            return lhs != null && lhs.value == rhs;
        }

        public static bool operator !=(BloomDebugViewParameter lhs, BloomDebugView rhs)
        {
            return !(lhs == rhs);
        }

        public override int GetHashCode()
        {
            return ((int)value * 31) + (overrideState ? 1 : 0);
        }

        public bool Equals(BloomDebugViewParameter other)
        {
            return other != null && value == other.value && overrideState == other.overrideState;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as BloomDebugViewParameter);
        }
    }

    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtExposureModeParameter")]
    public sealed class ExposureModeParameter : VolumeParameter<ExposureMode>
    {
        public ExposureModeParameter(ExposureMode value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable]
    public sealed class ColorGradingTemperatureModeParameter : VolumeParameter<ColorGradingTemperatureMode>
    {
        public ColorGradingTemperatureModeParameter(ColorGradingTemperatureMode value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }
}
