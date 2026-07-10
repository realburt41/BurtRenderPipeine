using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    [Serializable]
    [VolumeComponentMenu("Post Processing/Diaphragm Depth Of Field")]
    public sealed class DiaphragmDepthOfFieldVolumeComponent : VolumeComponent
    {
        [Title("Diaphragm Depth Of Field")]
        public BoolParameter enabled = new BoolParameter(true);

        [Tooltip("Distance in which the depth of field effect should be sharp, in centimeters.")]
        public ClampedFloatParameter focalDistance = new ClampedFloatParameter(DiaphragmDepthOfFieldSettings.DefaultFocalDistanceCm, 1f, 5000f);

        [Tooltip("Defines the opening of the camera lens. Larger numbers reduce the DOF effect.")]
        public ClampedFloatParameter fStop = new ClampedFloatParameter(DiaphragmDepthOfFieldSettings.DefaultFStop, 0.0001f, 16f);

        [Tooltip("Width of the camera sensor to assume, in millimeters.")]
        public ClampedFloatParameter sensorWidth = new ClampedFloatParameter(DiaphragmDepthOfFieldSettings.DefaultSensorWidthMm, 0.1f, 1000f);

        [Tooltip("Squeeze factor for anamorphic bokeh.")]
        public ClampedFloatParameter squeezeFactor = new ClampedFloatParameter(DiaphragmDepthOfFieldSettings.DefaultSqueezeFactor, 1f, 2f);

        [Tooltip("Circle DOF depth blur amount.")]
        public ClampedFloatParameter depthBlurAmount = new ClampedFloatParameter(DiaphragmDepthOfFieldSettings.DefaultDepthBlurAmount, 0.000001f, 100f);

        [Tooltip("Circle DOF depth blur radius in pixels at 1920x.")]
        public ClampedFloatParameter depthBlurRadius = new ClampedFloatParameter(DiaphragmDepthOfFieldSettings.DefaultDepthBlurRadius, 0f, 4f);

        [Tooltip("Matches the xrender field. The Burt implementation keeps a single gather path.")]
        public ClampedIntParameter recombineQuality = new ClampedIntParameter(0, 0, 2);

        public BoolParameter smoothGather = new BoolParameter(false);
        public BoolParameter visualizeDOF = new BoolParameter(false);

        public bool IsEnabled()
        {
            return active && HasAnyOverride() && enabled.value && focalDistance.value > 0f && fStop.value > 0f;
        }

        private bool HasAnyOverride()
        {
            return enabled.overrideState ||
                focalDistance.overrideState ||
                fStop.overrideState ||
                sensorWidth.overrideState ||
                squeezeFactor.overrideState ||
                depthBlurAmount.overrideState ||
                depthBlurRadius.overrideState ||
                recombineQuality.overrideState ||
                smoothGather.overrideState ||
                visualizeDOF.overrideState;
        }
    }

    internal readonly struct DiaphragmDepthOfFieldSettings
    {
        public const float DefaultFocalDistanceCm = 150f;
        public const float DefaultFStop = 1.2f;
        public const float DefaultSensorWidthMm = 24.576f;
        public const float DefaultSqueezeFactor = 1f;
        public const float DefaultDepthBlurAmount = 1f;
        public const float DefaultDepthBlurRadius = 0f;
        public const float MaxForegroundRadius = 0.025f;
        public const float MaxBackgroundRadius = 0.025f;
        private const float CmToMeters = 0.01f;
        private const float MmToMeters = 0.001f;
        private const float CompatibilityAspectRatio = 1.7777f;

        public static readonly DiaphragmDepthOfFieldSettings Default = new DiaphragmDepthOfFieldSettings(false);

        public bool Enabled { get; }
        public float FocusDistanceMeters { get; }
        public float InfinityBackgroundCocRadius { get; }
        public float MinForegroundCocRadius { get; }
        public float MaxBackgroundCocRadius { get; }
        public float DepthBlurExponent { get; }
        public float MaxDepthBlurRadius { get; }
        public float MaxRadiusPixels { get; }
        public float SqueezeFactor { get; }
        public bool SmoothGather { get; }
        public bool VisualizeDOF { get; }

        private DiaphragmDepthOfFieldSettings(bool enabled)
        {
            Enabled = enabled;
            FocusDistanceMeters = DefaultFocalDistanceCm * CmToMeters;
            InfinityBackgroundCocRadius = 0f;
            MinForegroundCocRadius = -MaxForegroundRadius;
            MaxBackgroundCocRadius = MaxBackgroundRadius;
            DepthBlurExponent = 1f / (DefaultDepthBlurAmount * 100000f);
            MaxDepthBlurRadius = DefaultDepthBlurRadius / 1920f * 2f;
            MaxRadiusPixels = 0f;
            SqueezeFactor = DefaultSqueezeFactor;
            SmoothGather = false;
            VisualizeDOF = false;
        }

        private DiaphragmDepthOfFieldSettings(
            bool enabled,
            float focusDistanceMeters,
            float infinityBackgroundCocRadius,
            float depthBlurExponent,
            float maxDepthBlurRadius,
            float maxRadiusPixels,
            float squeezeFactor,
            bool smoothGather,
            bool visualizeDOF)
        {
            Enabled = enabled;
            FocusDistanceMeters = Mathf.Max(0.0001f, focusDistanceMeters);
            InfinityBackgroundCocRadius = Mathf.Max(0f, SanitizeFinite(infinityBackgroundCocRadius, 0f));
            MinForegroundCocRadius = -MaxForegroundRadius;
            MaxBackgroundCocRadius = MaxBackgroundRadius;
            DepthBlurExponent = Mathf.Max(0.000000001f, SanitizeFinite(depthBlurExponent, 1f / 100000f));
            MaxDepthBlurRadius = Mathf.Clamp(SanitizeFinite(maxDepthBlurRadius, 0f), 0f, 1f);
            MaxRadiusPixels = Mathf.Clamp(SanitizeFinite(maxRadiusPixels, 0f), 0f, 96f);
            SqueezeFactor = Mathf.Clamp(SanitizeFinite(squeezeFactor, DefaultSqueezeFactor), 1f, 2f);
            SmoothGather = smoothGather;
            VisualizeDOF = visualizeDOF;
        }

        public static DiaphragmDepthOfFieldSettings Create(DiaphragmDepthOfFieldVolumeComponent component, Camera camera)
        {
            if (component == null || !component.IsEnabled() || camera == null)
            {
                return Default;
            }

            var focusDistanceMeters = Mathf.Max(0.0001f, component.focalDistance.value * CmToMeters);
            var fStop = Mathf.Max(0.0001f, component.fStop.value);
            var squeeze = Mathf.Clamp(component.squeezeFactor.value, 1f, 2f);
            var projectionY = Mathf.Max(Mathf.Abs(camera.projectionMatrix[1, 1]), 0.0001f);
            var verticalHalfFov = Mathf.Atan(1f / projectionY);
            var sensorWidthMeters = Mathf.Max(0.000001f, component.sensorWidth.value * MmToMeters);
            var sensorAspectRatio = CompatibilityAspectRatio / squeeze;
            var sensorHeightMeters = Mathf.Max(0.000001f, sensorWidthMeters / Mathf.Max(sensorAspectRatio, 0.0001f));
            var verticalFocalLength = Mathf.Max(0.000001f, 0.5f * sensorHeightMeters * (1f / Mathf.Tan(verticalHalfFov)));
            var infinityBackgroundCocRadius = 0f;
            if (focusDistanceMeters > verticalFocalLength)
            {
                var verticalDiameter = verticalFocalLength * verticalFocalLength / (fStop * Mathf.Max(focusDistanceMeters - verticalFocalLength, 0.000001f));
                var uncroppedVerticalInfinityBackgroundCocRadius = verticalDiameter * 0.5f / sensorHeightMeters;
                var desqueezedAspectRatio = sensorWidthMeters / sensorHeightMeters * squeeze;
                var verticalInfinityBackgroundCocRadius = uncroppedVerticalInfinityBackgroundCocRadius * Mathf.Max(CompatibilityAspectRatio / Mathf.Max(desqueezedAspectRatio, 0.0001f), 1f);
                infinityBackgroundCocRadius = verticalInfinityBackgroundCocRadius / CompatibilityAspectRatio;
            }

            var depthBlurAmount = Mathf.Max(0.000001f, component.depthBlurAmount.value);
            var depthBlurExponent = 1f / (depthBlurAmount * 100000f);
            var maxDepthBlurRadius = Mathf.Clamp(component.depthBlurRadius.value / 1920f * 2f, 0f, 1f);
            var width = Mathf.Max(1, camera.targetTexture != null ? camera.targetTexture.width : camera.pixelWidth);
            var maxRadiusPixels = Mathf.Min(96f, MaxBackgroundRadius * width * 0.5f);

            return new DiaphragmDepthOfFieldSettings(
                true,
                focusDistanceMeters,
                infinityBackgroundCocRadius,
                depthBlurExponent,
                maxDepthBlurRadius,
                maxRadiusPixels,
                squeeze,
                component.smoothGather.value,
                component.visualizeDOF.value);
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }
    }
}
