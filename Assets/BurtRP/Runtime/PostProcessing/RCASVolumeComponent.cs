using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    [Serializable]
    [VolumeComponentMenu("Post Processing/RCAS")]
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtRCASVolumeComponent")]
    public sealed class RCASVolumeComponent : VolumeComponent
    {
        [Title("RCAS")]
        [InfoBox("Runs after tonemapping and color grading as a final sharpening pass.")]
        public BoolParameter enabled = new BoolParameter(true);
        public ClampedFloatParameter sharpness = new ClampedFloatParameter(RCASSettings.DefaultSharpness, 0f, 1f);

        public bool IsEnabled()
        {
            return active && (enabled.overrideState || sharpness.overrideState) && enabled.value && sharpness.value > 0f;
        }
    }
}
