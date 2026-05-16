using System;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    [Serializable]
    public sealed class BurtTonemappingModeParameter : VolumeParameter<BurtTonemappingMode>
    {
        public BurtTonemappingModeParameter(BurtTonemappingMode value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable]
    public sealed class BurtBloomQualityParameter : VolumeParameter<BurtBloomQuality>, IEquatable<BurtBloomQualityParameter>
    {
        public BurtBloomQualityParameter(BurtBloomQuality value, bool overrideState = false)
            : base(value, overrideState)
        {
        }

        public static bool operator ==(BurtBloomQualityParameter lhs, BurtBloomQuality rhs)
        {
            return lhs != null && lhs.value == rhs;
        }

        public static bool operator !=(BurtBloomQualityParameter lhs, BurtBloomQuality rhs)
        {
            return !(lhs == rhs);
        }

        public override int GetHashCode()
        {
            return ((int)value * 31) + (overrideState ? 1 : 0);
        }

        public bool Equals(BurtBloomQualityParameter other)
        {
            return other != null && value == other.value && overrideState == other.overrideState;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as BurtBloomQualityParameter);
        }
    }

    [Serializable]
    public sealed class BurtBloomDebugViewParameter : VolumeParameter<BurtBloomDebugView>, IEquatable<BurtBloomDebugViewParameter>
    {
        public BurtBloomDebugViewParameter(BurtBloomDebugView value, bool overrideState = false)
            : base(value, overrideState)
        {
        }

        public static bool operator ==(BurtBloomDebugViewParameter lhs, BurtBloomDebugView rhs)
        {
            return lhs != null && lhs.value == rhs;
        }

        public static bool operator !=(BurtBloomDebugViewParameter lhs, BurtBloomDebugView rhs)
        {
            return !(lhs == rhs);
        }

        public override int GetHashCode()
        {
            return ((int)value * 31) + (overrideState ? 1 : 0);
        }

        public bool Equals(BurtBloomDebugViewParameter other)
        {
            return other != null && value == other.value && overrideState == other.overrideState;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as BurtBloomDebugViewParameter);
        }
    }

    [Serializable]
    public sealed class BurtExposureModeParameter : VolumeParameter<BurtExposureMode>
    {
        public BurtExposureModeParameter(BurtExposureMode value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }
}
