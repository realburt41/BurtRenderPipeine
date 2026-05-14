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
}
