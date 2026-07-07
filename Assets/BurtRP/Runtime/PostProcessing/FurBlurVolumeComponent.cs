using System;
using Sirenix.OdinInspector;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    [Serializable]
    [VolumeComponentMenu("Rendering/Fur Blur")]
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtFurBlurVolumeComponent")]
    public sealed class FurBlurVolumeComponent : VolumeComponent
    {
        [Title("BurtRP Fur Blur")]
        public BoolParameter enabled = new BoolParameter(true);
        public BoolParameter tiledBlur = new BoolParameter(false);
        public ClampedFloatParameter radiusCm = new ClampedFloatParameter(BurtFurBlurSettings.DefaultRadiusCm, 0f, 8f);
        public ClampedFloatParameter depthThresholdEye = new ClampedFloatParameter(BurtFurBlurSettings.DefaultDepthThresholdEye, 0.001f, 0.2f);
        public ClampedFloatParameter directionDilationThreshold = new ClampedFloatParameter(BurtFurBlurSettings.DefaultDirectionDilationThreshold, 0f, 1f);

        [Title("Temporal")]
        public BoolParameter thetaTemporal = new BoolParameter(BurtFurBlurSettings.DefaultThetaTemporal);
        public BoolParameter colorTemporal = new BoolParameter(BurtFurBlurSettings.DefaultColorTemporal);
        public ClampedFloatParameter thetaFeedback = new ClampedFloatParameter(BurtFurBlurSettings.DefaultThetaFeedback, 0f, 0.98f);
        public ClampedFloatParameter temporalFeedback = new ClampedFloatParameter(BurtFurBlurSettings.DefaultTemporalFeedback, 0f, 0.98f);
    }
}
