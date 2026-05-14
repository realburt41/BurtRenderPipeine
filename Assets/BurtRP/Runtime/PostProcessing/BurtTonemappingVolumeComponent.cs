using System;
using Sirenix.OdinInspector;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    [Serializable]
    [VolumeComponentMenu("BurtRP/Post Processing/Tonemapping")]
    public sealed class BurtTonemappingVolumeComponent : VolumeComponent
    {
        [Title("BurtRP Tonemapping")]
        [InfoBox("Tonemapping parameters are stored in Global Volume; BurtRenderPipelineAsset only controls the post-processing framework switch.")]
        public BurtTonemappingModeParameter mode = new BurtTonemappingModeParameter(BurtTonemappingMode.None);
        public ClampedFloatParameter postExposure = new ClampedFloatParameter(0f, -8f, 8f);

        [Title("UE / XRender Filmic Parameters")]
        [InfoBox("Default values match the XRender / UE Filmic tonemapper film settings.")]
        public ClampedFloatParameter filmSlope = new ClampedFloatParameter(BurtTonemappingFilmSettings.DefaultSlope, 0f, 1f);
        public ClampedFloatParameter filmToe = new ClampedFloatParameter(BurtTonemappingFilmSettings.DefaultToe, 0f, 1f);
        public ClampedFloatParameter filmShoulder = new ClampedFloatParameter(BurtTonemappingFilmSettings.DefaultShoulder, 0f, 1f);
        public ClampedFloatParameter filmBlackClip = new ClampedFloatParameter(BurtTonemappingFilmSettings.DefaultBlackClip, 0f, 1f);
        public ClampedFloatParameter filmWhiteClip = new ClampedFloatParameter(BurtTonemappingFilmSettings.DefaultWhiteClip, 0f, 1f);

        [Title("XRender LUT Compatibility")]
        public ClampedFloatParameter blueCorrection = new ClampedFloatParameter(BurtTonemappingFilmSettings.DefaultBlueCorrection, 0f, 1f);
        public ClampedFloatParameter expandGamut = new ClampedFloatParameter(BurtTonemappingFilmSettings.DefaultExpandGamut, 0f, 1f);
        public ClampedFloatParameter toneCurveAmount = new ClampedFloatParameter(BurtTonemappingFilmSettings.DefaultToneCurveAmount, 0f, 1f);

        public bool IsEnabled()
        {
            return active && mode.value != BurtTonemappingMode.None;
        }
    }
}
