using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    [Serializable]
    [VolumeComponentMenu("Post Processing/SMAA")]
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtSubpixelMorphologicalAAVolumeComponent")]
    public sealed class SubpixelMorphologicalAAVolumeComponent : VolumeComponent
    {
        [Title("SMAA")]
        public ClampedFloatParameter threshold = new ClampedFloatParameter(SubpixelMorphologicalAASettings.DefaultThreshold, 0.02f, 0.25f);
        public ClampedFloatParameter blendStrength = new ClampedFloatParameter(SubpixelMorphologicalAASettings.DefaultBlendStrength, 0f, 1f);
        public ClampedIntParameter maxSearchSteps = new ClampedIntParameter(SubpixelMorphologicalAASettings.DefaultMaxSearchSteps, 1, 16);

        public bool IsEnabled()
        {
            return active && blendStrength.value > 0f;
        }
    }
}
