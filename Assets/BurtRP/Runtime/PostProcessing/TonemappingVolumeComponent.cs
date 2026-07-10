using System;
using Sirenix.OdinInspector;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    [Serializable]
    [VolumeComponentMenu("Post Processing/Tonemapping")]
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtTonemappingVolumeComponent")]
    public sealed class TonemappingVolumeComponent : VolumeComponent
    {
        [Title("BurtRP Tonemapping")]
        [InfoBox("Tonemapping parameters are stored in Global Volume; BurtRenderPipelineAsset only controls the post-processing framework switch.")]
        public BoolParameter enabled = new BoolParameter(true);
        public TonemappingModeParameter mode = new TonemappingModeParameter(TonemappingMode.None);

        [Title("UE / XRender Filmic Parameters")]
        [InfoBox("Default values match the XRender / UE Filmic tonemapper film settings.")]
        public ClampedFloatParameter filmSlope = new ClampedFloatParameter(TonemappingFilmSettings.DefaultSlope, 0f, 1f);
        public ClampedFloatParameter filmToe = new ClampedFloatParameter(TonemappingFilmSettings.DefaultToe, 0f, 1f);
        public ClampedFloatParameter filmShoulder = new ClampedFloatParameter(TonemappingFilmSettings.DefaultShoulder, 0f, 1f);
        public ClampedFloatParameter filmBlackClip = new ClampedFloatParameter(TonemappingFilmSettings.DefaultBlackClip, 0f, 1f);
        public ClampedFloatParameter filmWhiteClip = new ClampedFloatParameter(TonemappingFilmSettings.DefaultWhiteClip, 0f, 1f);

        [Title("XRender LUT Compatibility")]
        public ClampedFloatParameter blueCorrection = new ClampedFloatParameter(TonemappingFilmSettings.DefaultBlueCorrection, 0f, 1f);
        public ClampedFloatParameter expandGamut = new ClampedFloatParameter(TonemappingFilmSettings.DefaultExpandGamut, 0f, 1f);
        public ClampedFloatParameter toneCurveAmount = new ClampedFloatParameter(TonemappingFilmSettings.DefaultToneCurveAmount, 0f, 1f);

        public bool IsEnabled()
        {
            return active && enabled.value && mode.value != TonemappingMode.None;
        }
    }
}
