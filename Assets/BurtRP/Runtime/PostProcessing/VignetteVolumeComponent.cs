using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    [Serializable]
    [VolumeComponentMenu("Post Processing/Vignette")]
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtVignetteVolumeComponent")]
    public sealed class VignetteVolumeComponent : VolumeComponent
    {
        private const float ActiveEpsilon = 0.0001f;

        [Title("BurtRP Vignette")]
        [InfoBox("Runs inside the Burt post-process copy/composite pass after tonemapping and color adjustments.")]
        public ColorParameter color = new ColorParameter(VignetteSettings.DefaultColor, true, true, true);
        public ClampedFloatParameter intensity = new ClampedFloatParameter(VignetteSettings.DefaultIntensity, 0f, 1f);
        public ClampedFloatParameter edgeWidth = new ClampedFloatParameter(VignetteSettings.DefaultEdgeWidth, 0f, 2f);
        public ClampedFloatParameter edgeSoftness = new ClampedFloatParameter(VignetteSettings.DefaultEdgeSoftness, 0f, 0.75f);

        [Title("Fisheye")]
        public ClampedFloatParameter fisheyeFovDeg = new ClampedFloatParameter(VignetteSettings.DefaultFisheyeFovDeg, 0f, 90f);
        public BoolParameter followAspect = new BoolParameter(VignetteSettings.DefaultFollowAspect);

        public bool IsEnabled()
        {
            return active && (intensity.value > ActiveEpsilon || fisheyeFovDeg.value > ActiveEpsilon);
        }
    }

    internal readonly struct VignetteSettings
    {
        public const float DefaultIntensity = 0f;
        public const float DefaultEdgeWidth = 0.37f;
        public const float DefaultEdgeSoftness = 0.3f;
        public const float DefaultFisheyeFovDeg = 0f;
        public const bool DefaultFollowAspect = true;
        public static readonly Color DefaultColor = new Color(0f, 0f, 0f, 1f);
        public static readonly VignetteSettings Default = new VignetteSettings(false, DefaultColor, DefaultIntensity, DefaultEdgeWidth, DefaultEdgeSoftness, DefaultFisheyeFovDeg, DefaultFollowAspect);

        public bool Enabled { get; }
        public Color Color { get; }
        public float Intensity { get; }
        public float EdgeWidth { get; }
        public float EdgeSoftness { get; }
        public float FisheyeFovDeg { get; }
        public bool FollowAspect { get; }

        public VignetteSettings(
            bool enabled,
            Color color,
            float intensity,
            float edgeWidth,
            float edgeSoftness,
            float fisheyeFovDeg,
            bool followAspect)
        {
            Enabled = enabled;
            Color = SanitizeColor(color);
            Intensity = Mathf.Clamp01(SanitizeFinite(intensity, DefaultIntensity));
            EdgeWidth = Mathf.Clamp(SanitizeFinite(edgeWidth, DefaultEdgeWidth), 0f, 2f);
            EdgeSoftness = Mathf.Clamp(SanitizeFinite(edgeSoftness, DefaultEdgeSoftness), 0.01f, 0.75f);
            FisheyeFovDeg = Mathf.Clamp(SanitizeFinite(fisheyeFovDeg, DefaultFisheyeFovDeg), 0f, 90f);
            FollowAspect = followAspect;
        }

        private static Color SanitizeColor(Color value)
        {
            return new Color(
                Mathf.Clamp(SanitizeFinite(value.r, DefaultColor.r), 0f, 65504f),
                Mathf.Clamp(SanitizeFinite(value.g, DefaultColor.g), 0f, 65504f),
                Mathf.Clamp(SanitizeFinite(value.b, DefaultColor.b), 0f, 65504f),
                Mathf.Clamp01(SanitizeFinite(value.a, DefaultColor.a)));
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }
    }
}
